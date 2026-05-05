using System;
using System.Collections.Generic;
using UnityEngine;

public abstract partial class TilingMap : Map
{
    protected virtual float VertexQuantizeScale => Mathf.Max(1f, 1f / cellSize) * 1000f;

    // 拾取桶大小：默认用 cellSize，可按地图类型覆写
    protected virtual float PickBucketSize => Mathf.Max(1e-4f, cellSize);

    // 是否启用低度点剥离（2-core）
    protected virtual bool EnableLowDegreePrune => true;

    // 可玩图最小度数；2 表示去掉度 <= 1 的点
    protected virtual int MinPlayableDegree => 2;

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

        public override int GetHashCode()
        {
            unchecked
            {
                int hx = (int)(x ^ (x >> 32));
                int hy = (int)(y ^ (y >> 32));
                return (hx * 397) ^ hy;
            }
        }
    }

    private struct BucketKey : IEquatable<BucketKey>
    {
        public readonly int x;
        public readonly int y;

        public BucketKey(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(BucketKey other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is BucketKey k && Equals(k);
        public override int GetHashCode() => (x * 397) ^ y;
    }

    private readonly Dictionary<BucketKey, List<Cell>> pickBuckets = new();

    // 复用容器，减少 BuildNeighbours() 的临时分配
    private readonly Dictionary<VertexKey, List<Cell>> vertexMap = new();
    private readonly Stack<List<Cell>> listPool = new();

    // 邻接去重：比 HashSet<ulong> 更轻量
    private int[] neighbourSeenStamp = Array.Empty<int>();
    private int currentSeenStamp;

    private List<Cell> RentList(int capacity)
    {
        if (listPool.Count > 0)
        {
            var list = listPool.Pop();
            if (list.Capacity < capacity)
            {
                list.Capacity = capacity;
            }

            return list;
        }

        return new List<Cell>(capacity);
    }

    private void ReturnList(List<Cell> list)
    {
        list.Clear();
        listPool.Push(list);
    }

    private void ClearAndRecycle(Dictionary<VertexKey, List<Cell>> dict)
    {
        foreach (var kv in dict)
        {
            ReturnList(kv.Value);
        }

        dict.Clear();
    }

    private void ClearAndRecycle(Dictionary<BucketKey, List<Cell>> dict)
    {
        foreach (var kv in dict)
        {
            ReturnList(kv.Value);
        }

        dict.Clear();
    }

    protected virtual Vector2[] GetCellVertices(Cell c)
    {
        if (!c.geometryDirty && c.cachedWorldVertices != null)
        {
            return c.cachedWorldVertices;
        }

        var local = PoolManager.instance.GetSharedLocalVertices(c.shapeType);
        int count = local.Count;

        var worldVerts = c.cachedWorldVertices;
        if (worldVerts == null || worldVerts.Length != count)
        {
            worldVerts = new Vector2[count];
            c.cachedWorldVertices = worldVerts;
        }

        Vector3 pos3 = c.position;
        float px = pos3.x;
        float py = pos3.y;
        float s = c.scale;

        Quaternion rot = c.rotation;
        float sin = 2f * (rot.w * rot.z);
        float cos = 1f - 2f * (rot.z * rot.z);
        bool noRotation = Mathf.Abs(sin) < 1e-6f && Mathf.Abs(cos - 1f) < 1e-6f;

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        if (noRotation)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 lv = local[i];
                float wx = px + lv.x * s;
                float wy = py + lv.y * s;

                worldVerts[i] = new Vector2(wx, wy);

                if (wx < minX)
                {
                    minX = wx;
                }

                if (wy < minY)
                {
                    minY = wy;
                }

                if (wx > maxX)
                {
                    maxX = wx;
                }

                if (wy > maxY)
                {
                    maxY = wy;
                }
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 lv = local[i];
                float vx = lv.x * s;
                float vy = lv.y * s;

                float wx = px + vx * cos - vy * sin;
                float wy = py + vx * sin + vy * cos;

                worldVerts[i] = new Vector2(wx, wy);

                if (wx < minX)
                {
                    minX = wx;
                }

                if (wy < minY)
                {
                    minY = wy;
                }

                if (wx > maxX)
                {
                    maxX = wx;
                }

                if (wy > maxY)
                {
                    maxY = wy;
                }
            }
        }

        c.cachedAabb.SetMinMax(
            new Vector3(minX, minY, 0f),
            new Vector3(maxX, maxY, 0f)
        );

        c.geometryDirty = false;
        return worldVerts;
    }

    public override bool TryGetCellAtWorld(Vector2 worldPos, out Cell cell)
    {
        float bucketSize = PickBucketSize;
        int bx = Mathf.FloorToInt(worldPos.x / bucketSize);
        int by = Mathf.FloorToInt(worldPos.y / bucketSize);

        float px = worldPos.x;
        float py = worldPos.y;

        // 查 3x3 邻域，避免边界浮点误差漏检
        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                var key = new BucketKey(bx + ox, by + oy);
                if (!pickBuckets.TryGetValue(key, out var candidates))
                {
                    continue;
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    var c = candidates[i];
                    if (c.isBorder)
                    {
                        continue;
                    }

                    var verts = GetCellVertices(c);

                    var b = c.cachedAabb;
                    if (px < b.min.x || px > b.max.x || py < b.min.y || py > b.max.y)
                    {
                        continue;
                    }

                    if (IsPointInPolygon(worldPos, verts))
                    {
                        cell = c;
                        return true;
                    }
                }
            }
        }

        cell = null;
        return false;
    }

    protected override void BuildNeighbours()
    {
        // 1) 只构一次完整邻接
        BuildAdjacencyOnly();

        // 2) 在当前邻接上做 2-core 剥离
        if (EnableLowDegreePrune && MinPlayableDegree > 0
            && PruneLowDegreeCellsToBorder(MinPlayableDegree, out bool[] removed))
        {
            // 3) 不重构邻接，直接原地清理并压缩 cellList
            CompactCellListAndCleanupNeighbours(removed);
        }

        // 4) 最后基于最终 cellList 构建拾取桶
        RebuildPickBuckets();
    }

    private void BuildAdjacencyOnly()
    {
        ClearAndRecycle(vertexMap);

        int cellCount = cellList.Count;
        if (cellCount == 0)
        {
            return;
        }

        if (neighbourSeenStamp.Length < cellCount)
        {
            neighbourSeenStamp = new int[cellCount];
        }

        float quantizeScale = VertexQuantizeScale;

        for (int ci = 0; ci < cellCount; ci++)
        {
            var c = cellList[ci];
            c.tempBuildIndex = ci;

            var neighbours = c.neighbours;
            neighbours.Clear();

            // 轻量预扩容，减少 neighbours.Add() 触发扩容
            // 多数平铺图邻居数不高，8 是比较稳妥的经验值
            if (neighbours.Capacity < 8)
            {
                neighbours.Capacity = 8;
            }

            currentSeenStamp++;
            if (currentSeenStamp == int.MaxValue)
            {
                Array.Clear(neighbourSeenStamp, 0, cellCount);
                currentSeenStamp = 1;
            }

            int stamp = currentSeenStamp;
            var verts = GetCellVertices(c);
            int vertCount = verts.Length;

            // 一边建 vertexMap，一边连接邻接
            for (int i = 0; i < vertCount; i++)
            {
                Vector2 v = verts[i];
                var key = new VertexKey(
                    (long)Mathf.Round(v.x * quantizeScale),
                    (long)Mathf.Round(v.y * quantizeScale)
                );

                if (!vertexMap.TryGetValue(key, out var list))
                {
                    list = RentList(4);
                    vertexMap[key] = list;
                }

                for (int j = 0, listCount = list.Count; j < listCount; j++)
                {
                    var other = list[j];
                    if (other == c)
                    {
                        continue;
                    }

                    int otherIndex = other.tempBuildIndex;
                    if (neighbourSeenStamp[otherIndex] == stamp)
                    {
                        continue;
                    }

                    neighbourSeenStamp[otherIndex] = stamp;
                    neighbours.Add(other);
                    other.neighbours.Add(c);
                }

                list.Add(c);
            }
        }

        ClearAndRecycle(vertexMap);
    }

    private bool PruneLowDegreeCellsToBorder(int minDegree, out bool[] removed)
    {
        int cellCount = cellList.Count;
        if (cellCount == 0)
        {
            removed = Array.Empty<bool>();
            return false;
        }

        var degree = new int[cellCount];
        removed = new bool[cellCount];
        var queue = new Queue<int>(cellCount);

        for (int i = 0; i < cellCount; i++)
        {
            var c = cellList[i];
            c.tempBuildIndex = i;
            degree[i] = c.neighbours.Count;

            if (degree[i] < minDegree)
            {
                queue.Enqueue(i);
            }
        }

        int removedCount = 0;

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            if (removed[idx])
            {
                continue;
            }

            removed[idx] = true;
            removedCount++;

            var c = cellList[idx];
            var neighbours = c.neighbours;

            for (int i = 0; i < neighbours.Count; i++)
            {
                var n = neighbours[i];
                int ni = n.tempBuildIndex;
                if (ni < 0 || ni >= cellCount || removed[ni])
                {
                    continue;
                }

                degree[ni]--;
                if (degree[ni] < minDegree)
                {
                    queue.Enqueue(ni);
                }
            }
        }

        if (removedCount == 0)
        {
            return false;
        }

        for (int i = 0; i < cellCount; i++)
        {
            if (!removed[i])
            {
                continue;
            }

            var c = cellList[i];
            c.isBorder = true;
            c.isMine = false;
            c.value = 0;
            c.isFlagged = false;
            c.neighbours.Clear();
            c.tempBuildIndex = -1;
        }

        return true;
    }

    private void CompactCellListAndCleanupNeighbours(bool[] removed)
    {
        int oldCount = removed.Length;
        if (oldCount == 0)
        {
            return;
        }

        // 原地压缩 cellList（移除被剥离点）
        int write = 0;
        for (int read = 0; read < oldCount; read++)
        {
            if (removed[read])
            {
                continue;
            }

            cellList[write] = cellList[read];
            write++;
        }

        if (write < cellList.Count)
        {
            cellList.RemoveRange(write, cellList.Count - write);
        }

        // 原地清理保留节点的邻接：去掉已剥离点引用
        int keptCount = cellList.Count;
        for (int i = 0; i < keptCount; i++)
        {
            var c = cellList[i];
            var neighbours = c.neighbours;

            int nWrite = 0;
            for (int nRead = 0; nRead < neighbours.Count; nRead++)
            {
                var n = neighbours[nRead];
                int ni = n.tempBuildIndex;
                if (ni < 0 || ni >= oldCount || removed[ni])
                {
                    continue;
                }

                neighbours[nWrite] = n;
                nWrite++;
            }

            if (nWrite < neighbours.Count)
            {
                neighbours.RemoveRange(nWrite, neighbours.Count - nWrite);
            }
        }

        // 重新写入保留节点索引
        for (int i = 0; i < keptCount; i++)
        {
            cellList[i].tempBuildIndex = i;
        }
    }

    private void RebuildPickBuckets()
    {
        ClearAndRecycle(pickBuckets);

        int cellCount = cellList.Count;
        if (cellCount == 0)
        {
            return;
        }

        float bucketSize = PickBucketSize;
        float invBucketSize = 1f / bucketSize;

        for (int i = 0; i < cellCount; i++)
        {
            var c = cellList[i];
            var aabb = c.cachedAabb;

            int minX = Mathf.FloorToInt(aabb.min.x * invBucketSize);
            int maxX = Mathf.FloorToInt(aabb.max.x * invBucketSize);
            int minY = Mathf.FloorToInt(aabb.min.y * invBucketSize);
            int maxY = Mathf.FloorToInt(aabb.max.y * invBucketSize);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var bk = new BucketKey(x, y);
                    if (!pickBuckets.TryGetValue(bk, out var bucket))
                    {
                        bucket = RentList(8);
                        pickBuckets[bk] = bucket;
                    }

                    bucket.Add(c);
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