using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class TilingMap : Map
{
    protected virtual float VertexQuantizeScale => Mathf.Max(1f, 1f / cellSize) * 1000f;

    // 拾取桶大小：默认用 cellSize，可按地图类型覆写
    protected virtual float PickBucketSize => Mathf.Max(1e-4f, cellSize);

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

    protected VertexKey Quantize(Vector2 v)
    {
        long qx = (long)Mathf.Round(v.x * VertexQuantizeScale);
        long qy = (long)Mathf.Round(v.y * VertexQuantizeScale);
        return new VertexKey(qx, qy);
    }

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
        ClearAndRecycle(vertexMap);
        ClearAndRecycle(pickBuckets);

        int cellCount = cellList.Count;
        if (cellCount == 0)
        {
            return;
        }

        if (neighbourSeenStamp.Length < cellCount)
        {
            neighbourSeenStamp = new int[cellCount];
        }

        float bucketSize = PickBucketSize;
        float invBucketSize = 1f / bucketSize;
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

            // 建拾取桶（按 AABB 覆盖到多个桶）
            // 使用乘 invBucketSize 替代除法，减少热点内浮点除法成本
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

        ClearAndRecycle(vertexMap);
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

    #region CheckOverlap
    protected virtual float OverlapLinearTolerance => Mathf.Max(1e-5f, cellSize * 1e-3f);
    protected virtual float OverlapAabbTolerance => OverlapLinearTolerance;

    protected override void ValidateNoCellOverlapInEditor()
    {
#if UNITY_EDITOR
        int cellCount = cellList.Count;
        if (cellCount <= 1)
        {
            return;
        }

        ClearAndRecycle(pickBuckets);

        float bucketSize = PickBucketSize;
        float invBucketSize = 1f / bucketSize;
        float aabbTolerance = OverlapAabbTolerance;
        float overlapTolerance = OverlapLinearTolerance;

        var checkedPairs = new HashSet<ulong>();
        int overlapCount = 0;
        const int maxDetailedReport = 20;

        for (int i = 0; i < cellCount; i++)
        {
            var c = cellList[i];
            c.tempBuildIndex = i;
            GetCellVertices(c);

            var aabb = c.cachedAabb;
            int minX = Mathf.FloorToInt(aabb.min.x * invBucketSize);
            int maxX = Mathf.FloorToInt(aabb.max.x * invBucketSize);
            int minY = Mathf.FloorToInt(aabb.min.y * invBucketSize);
            int maxY = Mathf.FloorToInt(aabb.max.y * invBucketSize);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var key = new BucketKey(x, y);
                    if (!pickBuckets.TryGetValue(key, out var bucket))
                    {
                        bucket = RentList(8);
                        pickBuckets[key] = bucket;
                    }

                    for (int j = 0; j < bucket.Count; j++)
                    {
                        var other = bucket[j];
                        int oi = other.tempBuildIndex;

                        int minIndex = oi < i ? oi : i;
                        int maxIndex = oi < i ? i : oi;
                        ulong pairKey = ((ulong)(uint)minIndex << 32) | (uint)maxIndex;

                        if (!checkedPairs.Add(pairKey))
                        {
                            continue;
                        }

                        if (!CellsOverlapByArea(c, other, aabbTolerance, overlapTolerance))
                        {
                            continue;
                        }

                        overlapCount++;
                        c.name = "overlap";
                        other.name = "overlap";

                        if (overlapCount <= maxDetailedReport)
                        {
                            Debug.LogError(
                                $"[Map] 检测到 Cell 重叠: #{oi} ({other.position.x:F4}, {other.position.y:F4}) <-> #{i} ({c.position.x:F4}, {c.position.y:F4})",
                                this);
                        }
                    }

                    bucket.Add(c);
                }
            }
        }

        if (overlapCount > 0)
        {
            if (overlapCount > maxDetailedReport)
            {
                Debug.LogError($"[Map] 其余 {overlapCount - maxDetailedReport} 对重叠已省略输出。", this);
            }

            Debug.LogError($"[Map] Cell 重叠检测失败，共发现 {overlapCount} 对重叠。", this);
        }

        ClearAndRecycle(pickBuckets);
#endif
    }

    private static bool CellsOverlapByArea(Cell a, Cell b, float aabbTolerance, float overlapTolerance)
    {
        var aa = a.cachedAabb;
        var bb = b.cachedAabb;

        if (aa.max.x <= bb.min.x + aabbTolerance || bb.max.x <= aa.min.x + aabbTolerance
            || aa.max.y <= bb.min.y + aabbTolerance || bb.max.y <= aa.min.y + aabbTolerance)
        {
            return false;
        }

        var pa = a.cachedWorldVertices;
        var pb = b.cachedWorldVertices;

        if (pa == null || pb == null || pa.Length < 3 || pb.Length < 3)
        {
            return false;
        }

        return ConvexPolygonsOverlapByArea(pa, pb, overlapTolerance);
    }

    private static bool ConvexPolygonsOverlapByArea(Vector2[] a, Vector2[] b, float overlapTolerance)
    {
        return !HasSeparatingAxisOrWithinTolerance(a, b, overlapTolerance)
            && !HasSeparatingAxisOrWithinTolerance(b, a, overlapTolerance);
    }

    private static bool HasSeparatingAxisOrWithinTolerance(Vector2[] axisSource, Vector2[] target, float overlapTolerance)
    {
        const float minAxisLen = 1e-6f;

        int n = axisSource.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = axisSource[i];
            Vector2 p1 = axisSource[(i + 1) % n];
            Vector2 edge = p1 - p0;

            float nx = -edge.y;
            float ny = edge.x;
            float len = Mathf.Sqrt(nx * nx + ny * ny);
            if (len <= minAxisLen)
            {
                continue;
            }

            nx /= len;
            ny /= len;

            Project(axisSource, nx, ny, out float minA, out float maxA);
            Project(target, nx, ny, out float minB, out float maxB);

            // 小于等于容差的“重叠”按不重叠处理（容错）
            if (maxA <= minB + overlapTolerance || maxB <= minA + overlapTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static void Project(Vector2[] polygon, float ax, float ay, out float min, out float max)
    {
        float v0 = polygon[0].x * ax + polygon[0].y * ay;
        min = v0;
        max = v0;

        for (int i = 1; i < polygon.Length; i++)
        {
            float v = polygon[i].x * ax + polygon[i].y * ay;
            if (v < min)
            {
                min = v;
            }

            if (v > max)
            {
                max = v;
            }
        }
    }
    #endregion
}