using System;
using System.Collections.Generic;
using UnityEngine;

public abstract partial class TilingMap : Map
{
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
}