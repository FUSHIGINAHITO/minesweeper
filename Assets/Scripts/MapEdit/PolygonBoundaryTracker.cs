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

    public int PlacedTileCount => placedTiles.Count;
    public IReadOnlyList<BoundaryEdge> BoundaryEdges => boundaryEdges;

    public void AddPlacedTile(int tileIndex, Vector2 position, float rotationDeg)
    {
        placedTiles.Add(new PlacedTileData
        {
            tileIndex = tileIndex,
            position = position,
            rotationDeg = rotationDeg
        });

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
}