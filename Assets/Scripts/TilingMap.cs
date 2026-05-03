using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class TilingMap : Map
{
    protected virtual float VertexQuantizeScale => Mathf.Max(1f, 1f / cellSize) * 1000f;

    protected struct VertexKey : IEquatable<VertexKey>
    {
        public readonly long x;
        public readonly long y;

        public VertexKey(long x, long y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(VertexKey other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is VertexKey k && Equals(k);
        public override int GetHashCode() => ((int)(x * 397)) ^ (int)y;
    }

    protected VertexKey Quantize(Vector2 v)
    {
        long qx = (long)Mathf.Round(v.x * VertexQuantizeScale);
        long qy = (long)Mathf.Round(v.y * VertexQuantizeScale);
        return new VertexKey(qx, qy);
    }

    protected virtual Vector2[] GetCellVertices(Cell c)
    {
        if (!c.geometryDirty && c.cachedWorldVertices != null)
        {
            return c.cachedWorldVertices;
        }

        var local = PoolManager.instance.GetSharedLocalVertices(c.shapeType);
        if (local == null || local.Count == 0)
        {
            c.cachedWorldVertices = Array.Empty<Vector2>();
            c.cachedAabb = new Bounds(c.transform.position, Vector3.zero);
            c.geometryDirty = false;
            return c.cachedWorldVertices;
        }

        if (c.cachedWorldVertices == null || c.cachedWorldVertices.Length != local.Count)
        {
            c.cachedWorldVertices = new Vector2[local.Count];
        }

        Vector3 p3 = c.transform.position;
        Vector2 p = new(p3.x, p3.y);
        float s = c.transform.localScale.x;
        Quaternion rot = c.transform.rotation;

        float sin = 2f * (rot.w * rot.z);
        float cos = 1f - 2f * (rot.z * rot.z);
        bool noRotation = Mathf.Abs(sin) < 1e-6f && Mathf.Abs(cos - 1f) < 1e-6f;

        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < local.Count; i++)
        {
            Vector2 v = local[i] * s;
            Vector2 w = noRotation
                ? p + v
                : new Vector2(
                    p.x + v.x * cos - v.y * sin,
                    p.y + v.x * sin + v.y * cos
                );

            c.cachedWorldVertices[i] = w;
            min = Vector2.Min(min, w);
            max = Vector2.Max(max, w);
        }

        Vector3 center = new((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
        Vector3 size = new(max.x - min.x, max.y - min.y, 0f);
        c.cachedAabb = new Bounds(center, size);

        c.geometryDirty = false;
        return c.cachedWorldVertices;
    }

    public override bool TryGetCellAtWorld(Vector2 worldPos, out Cell cell)
    {
        for (int i = 0; i < cellList.Count; i++)
        {
            var c = cellList[i];
            var verts = GetCellVertices(c);
            if (verts.Length == 0)
            {
                continue;
            }

            if (!c.cachedAabb.Contains(new Vector3(worldPos.x, worldPos.y, 0f)))
            {
                continue;
            }

            if (IsPointInPolygon(worldPos, verts))
            {
                cell = c;
                return true;
            }
        }

        cell = null;
        return false;
    }

    protected override void BuildNeighbours()
    {
        var vertexMap = new Dictionary<VertexKey, List<Cell>>();

        foreach (var c in cellList)
        {
            var verts = GetCellVertices(c);

            foreach (var v in verts)
            {
                var key = Quantize(v);
                if (!vertexMap.TryGetValue(key, out var list))
                {
                    list = new List<Cell>();
                    vertexMap[key] = list;
                }

                list.Add(c);
            }
        }

        foreach (var kv in vertexMap)
        {
            var list = kv.Value;
            int m = list.Count;
            for (int i = 0; i < m; i++)
            {
                for (int j = i + 1; j < m; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    if (a == b)
                    {
                        continue;
                    }

                    if (!a.neighbours.Contains(b))
                    {
                        a.neighbours.Add(b);
                    }

                    if (!b.neighbours.Contains(a))
                    {
                        b.neighbours.Add(a);
                    }
                }
            }
        }
    }

    private static bool IsPointInPolygon(Vector2 p, Vector2[] polygon)
    {
        bool inside = false;
        int n = polygon.Length;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];

            bool intersect = ((a.y > p.y) != (b.y > p.y))
                && (p.x < (b.x - a.x) * (p.y - a.y) / ((b.y - a.y) + 1e-12f) + a.x);

            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}