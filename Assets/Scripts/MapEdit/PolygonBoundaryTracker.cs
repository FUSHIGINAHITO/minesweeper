using System.Collections.Generic;
using UnityEngine;

public sealed class PolygonBoundaryTracker
{
    private readonly struct EdgeKey
    {
        public readonly long ax;
        public readonly long ay;
        public readonly long bx;
        public readonly long by;

        public EdgeKey(Vector2 p0, Vector2 p1, float q)
        {
            long x0 = Quantize(p0.x, q);
            long y0 = Quantize(p0.y, q);
            long x1 = Quantize(p1.x, q);
            long y1 = Quantize(p1.y, q);

            bool swap = x0 > x1 || (x0 == x1 && y0 > y1);
            if (swap)
            {
                ax = x1;
                ay = y1;
                bx = x0;
                by = y0;
            }
            else
            {
                ax = x0;
                ay = y0;
                bx = x1;
                by = y1;
            }
        }

        private static long Quantize(float v, float q)
        {
            return (long)Mathf.Round(v * q);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h0 = (int)(ax ^ (ax >> 32));
                int h1 = (int)(ay ^ (ay >> 32));
                int h2 = (int)(bx ^ (bx >> 32));
                int h3 = (int)(by ^ (by >> 32));
                return (((h0 * 397) ^ h1) * 397 ^ h2) * 397 ^ h3;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is not EdgeKey other)
            {
                return false;
            }

            return ax == other.ax && ay == other.ay && bx == other.bx && by == other.by;
        }
    }

    private struct EdgeCounter
    {
        public int count;
        public Vector2 a;
        public Vector2 b;
    }

    private readonly List<PlacedTileData> placedTiles = new();
    private readonly List<BoundaryEdge> boundaryEdges = new();
    private bool boundaryDirty = true;

    private const float PositionStoreQuantizeScale = 100000f;
    private const float RotationStoreQuantizeScale = 10000f;

    public int PlacedTileCount => placedTiles.Count;
    public IReadOnlyList<BoundaryEdge> BoundaryEdges => boundaryEdges;

    public void AddPlacedTile(int tileIndex, Vector2 position, float rotationDeg)
    {
        position = QuantizeVec2(position, PositionStoreQuantizeScale);
        rotationDeg = QuantizeFloat(rotationDeg, RotationStoreQuantizeScale);

        placedTiles.Add(new PlacedTileData
        {
            tileIndex = tileIndex,
            position = position,
            rotationDeg = rotationDeg
        });

        boundaryDirty = true;
    }

    public List<PlacedTileData> GetPlacedTilesSnapshot()
    {
        return new List<PlacedTileData>(placedTiles);
    }

    public void Clear()
    {
        placedTiles.Clear();
        boundaryEdges.Clear();
        boundaryDirty = true;
    }

    public void EnsureBoundaryEdges(MainDataSO mainDataSO, float cellScale, float edgeQuantizeScale)
    {
        if (!boundaryDirty)
        {
            return;
        }

        RebuildBoundaryEdges(mainDataSO, cellScale, edgeQuantizeScale);
    }

    public bool OverlapsAnyPlaced(int tileIndex, Vector2 position, float rotationDeg, MainDataSO mainDataSO, float cellScale)
    {
        return TryGetOverlapPair(tileIndex, position, rotationDeg, mainDataSO, cellScale, out _, out _, out _);
    }

    public bool TryGetOverlapPair(
        int tileIndex,
        Vector2 position,
        float rotationDeg,
        MainDataSO mainDataSO,
        float cellScale,
        out Vector2[] candidatePolygon,
        out Vector2[] placedPolygon,
        out float overlapArea)
    {
        candidatePolygon = null;
        placedPolygon = null;
        overlapArea = 0f;

        if (placedTiles.Count == 0)
        {
            return false;
        }

        position = QuantizeVec2(position, PositionStoreQuantizeScale);
        rotationDeg = QuantizeFloat(rotationDeg, RotationStoreQuantizeScale);

        TileSO tile = mainDataSO.tiles[tileIndex];
        candidatePolygon = BuildWorldVertices(tile, position, rotationDeg, cellScale);
        Rect candidateAabb = BuildAabb(candidatePolygon);

        float bestArea = 0f;
        Vector2[] bestOther = null;

        for (int i = 0; i < placedTiles.Count; i++)
        {
            PlacedTileData d = placedTiles[i];
            TileSO otherTile = mainDataSO.tiles[d.tileIndex];
            Vector2[] other = BuildWorldVertices(otherTile, d.position, d.rotationDeg, cellScale);

            Rect otherAabb = BuildAabb(other);
            if (!candidateAabb.Overlaps(otherAabb, true))
            {
                continue;
            }

            float area = EstimateOverlapArea(candidatePolygon, other, 56);
            if (area > bestArea)
            {
                bestArea = area;
                bestOther = other;
            }
        }

        if (bestOther == null)
        {
            return false;
        }

        placedPolygon = bestOther;
        overlapArea = bestArea;
        return true;
    }

    private static float EstimateOverlapArea(Vector2[] a, Vector2[] b, int samplesPerAxis)
    {
        Rect ra = BuildAabb(a);
        Rect rb = BuildAabb(b);

        float xMin = Mathf.Max(ra.xMin, rb.xMin);
        float xMax = Mathf.Min(ra.xMax, rb.xMax);
        float yMin = Mathf.Max(ra.yMin, rb.yMin);
        float yMax = Mathf.Min(ra.yMax, rb.yMax);

        float w = xMax - xMin;
        float h = yMax - yMin;
        if (w <= 1e-6f || h <= 1e-6f)
        {
            return 0f;
        }

        int nxBase = Mathf.Max(8, samplesPerAxis);
        float aspect = Mathf.Clamp(h / w, 0.25f, 4f);

        int nx = Mathf.Clamp(nxBase, 8, 128);
        int ny = Mathf.Clamp(Mathf.RoundToInt(nxBase * aspect), 8, 128);

        float dx = w / nx;
        float dy = h / ny;
        float cellArea = dx * dy;

        int insideCount = 0;
        for (int iy = 0; iy < ny; iy++)
        {
            float py = yMin + (iy + 0.5f) * dy;
            for (int ix = 0; ix < nx; ix++)
            {
                float px = xMin + (ix + 0.5f) * dx;
                Vector2 p = new Vector2(px, py);

                if (PointInPolygon(p, a) && PointInPolygon(p, b))
                {
                    insideCount++;
                }
            }
        }

        return insideCount * cellArea;
    }

    private static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 pi = poly[i];
            Vector2 pj = poly[j];

            bool intersect = ((pi.y > p.y) != (pj.y > p.y))
                && (p.x < (pj.x - pi.x) * (p.y - pi.y) / ((pj.y - pi.y) + 1e-12f) + pi.x);

            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static Rect BuildAabb(Vector2[] verts)
    {
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector2 p = verts[i];
            if (p.x < minX)
                minX = p.x;
            if (p.y < minY)
                minY = p.y;
            if (p.x > maxX)
                maxX = p.x;
            if (p.y > maxY)
                maxY = p.y;
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private void RebuildBoundaryEdges(MainDataSO mainDataSO, float cellScale, float edgeQuantizeScale)
    {
        var map = new Dictionary<EdgeKey, EdgeCounter>();
        boundaryEdges.Clear();

        for (int i = 0; i < placedTiles.Count; i++)
        {
            PlacedTileData d = placedTiles[i];
            TileSO tile = mainDataSO.tiles[d.tileIndex];
            Vector2[] verts = BuildWorldVertices(tile, d.position, d.rotationDeg, cellScale);

            int n = verts.Length;
            for (int e = 0; e < n; e++)
            {
                Vector2 a = verts[e];
                Vector2 b = verts[(e + 1) % n];

                EdgeKey key = new EdgeKey(a, b, edgeQuantizeScale);
                if (map.TryGetValue(key, out EdgeCounter c))
                {
                    c.count++;
                    map[key] = c;
                }
                else
                {
                    map.Add(key, new EdgeCounter
                    {
                        count = 1,
                        a = a,
                        b = b
                    });
                }
            }
        }

        foreach (var kv in map)
        {
            if (kv.Value.count == 1)
            {
                boundaryEdges.Add(new BoundaryEdge
                {
                    a = kv.Value.a,
                    b = kv.Value.b
                });
            }
        }

        boundaryDirty = false;
    }

    private static Vector2[] BuildWorldVertices(TileSO tile, Vector2 pos, float rotDeg, float cellScale)
    {
        Vector2[] src = tile.localVertices;
        int n = src.Length;
        Vector2[] dst = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            Vector2 p = src[i] * cellScale;
            dst[i] = PolygonSnapSolver.Rotate(p, rotDeg) + pos;
        }

        return dst;
    }

    private static float QuantizeFloat(float v, float scale)
    {
        if (scale <= 0f)
        {
            return v;
        }

        return Mathf.Round(v * scale) / scale;
    }

    private static Vector2 QuantizeVec2(Vector2 v, float scale)
    {
        if (scale <= 0f)
        {
            return v;
        }

        return new Vector2(
            Mathf.Round(v.x * scale) / scale,
            Mathf.Round(v.y * scale) / scale);
    }
}