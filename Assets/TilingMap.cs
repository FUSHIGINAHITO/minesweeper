using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class TilingMap : Map
{
    // 根据 cellSize 自动调整量化精度，子类可重写
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

    // 子类必须实现：返回该 cell 的顶点（世界坐标），顺序/闭合不强制（不要重复首尾）
    protected abstract Vector2[] GetCellVertices(Cell c);

    // 通用的基于“共享顶点”为邻居建立逻辑
    protected override void BuildNeighbours()
    {
        var vertexMap = new Dictionary<VertexKey, List<Cell>>();

        // 收集所有 cell 的顶点映射到 vertexMap
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

        // 对每个顶点上的 cell 列表，两两配对建立邻居关系（去重）
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
}