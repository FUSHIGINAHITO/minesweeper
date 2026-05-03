using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 阿基米德密铺 3.3.3.4.4
/// </summary>
public class Tile33344Map : TilingMap
{
    // 存放每个 Cell 对应的世界顶点（TilingMap 的 BuildNeighbours 使用 GetCellVertices）
    private readonly Dictionary<Cell, Vector2[]> placedVertices = new();

    protected override void GenerateGrid()
    {
        placedVertices.Clear();

        float s = cellSize;
        float half = s * 0.5f;
        float h = Mathf.Sqrt(3f) * 0.5f * s;
        float rowStep = s + h;

        Vector2[] protoTri = new Vector2[]
        {
        new Vector2(0f, 2f * h / 3f),
        new Vector2(s * 0.5f, -h / 3f),
        new Vector2(-s * 0.5f, -h / 3f)
        };

        Vector2[] protoSquare = new Vector2[]
        {
        new Vector2(-half, -half),
        new Vector2( half, -half),
        new Vector2( half,  half),
        new Vector2(-half,  half)
        };

        Rect viewRect = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);

        Vector2 worldCenter = new Vector2(
            (bottomLeft.x + topRight.x) * 0.5f,
            (bottomLeft.y + topRight.y) * 0.5f);

        var placedKeys = new HashSet<(long x, long y, TileType t)>();

        bool PolygonFullyInsideRect(Vector2[] verts, Rect rect)
        {
            for (int i = 0; i < verts.Length; i++)
            {
                if (!rect.Contains(verts[i]))
                {
                    return false;
                }
            }

            return true;
        }

        void AddSquare(Vector2 center)
        {
            long qx = (long)Mathf.Round(center.x * 10000f);
            long qy = (long)Mathf.Round(center.y * 10000f);
            var key = (qx, qy, TileType.Square);
            if (placedKeys.Contains(key))
            {
                return;
            }

            Vector2[] verts = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                verts[i] = center + protoSquare[i];
            }

            if (!PolygonFullyInsideRect(verts, viewRect))
            {
                return;
            }

            var cell = PoolManager.instance.square.Require();
            cell.Init(new(center.x, center.y, 0f), Quaternion.identity, s);

            cellList.Add(cell);
            placedVertices[cell] = verts;
            placedKeys.Add(key);
        }

        void AddTriangle(Vector2 centroid, float rotDeg)
        {
            long qx = (long)Mathf.Round(centroid.x * 10000f);
            long qy = (long)Mathf.Round(centroid.y * 10000f);
            var key = (qx, qy, TileType.Triangle);
            if (placedKeys.Contains(key))
            {
                return;
            }

            float rad = rotDeg * Mathf.Deg2Rad;
            float cr = Mathf.Cos(rad);
            float sr = Mathf.Sin(rad);

            Vector2[] verts = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = protoTri[i];
                Vector2 r = new Vector2(cr * p.x - sr * p.y, sr * p.x + cr * p.y);
                verts[i] = centroid + r;
            }

            if (!PolygonFullyInsideRect(verts, viewRect))
            {
                return;
            }

            var cell = PoolManager.instance.triangle.Require();
            cell.Init(new(centroid.x, centroid.y, 0f), Quaternion.Euler(0f, 0f, rotDeg), s);

            cellList.Add(cell);
            placedVertices[cell] = verts;
            placedKeys.Add(key);
        }

        // 方形行：仅生成完整方形
        int rSquareMin = Mathf.CeilToInt((viewRect.yMin + half - worldCenter.y) / rowStep);
        int rSquareMax = Mathf.FloorToInt((viewRect.yMax - half - worldCenter.y) / rowStep);

        for (int r = rSquareMin; r <= rSquareMax; r++)
        {
            float y = worldCenter.y + r * rowStep;
            float xOffset = ((r & 1) != 0) ? (s * 0.5f) : 0f;
            float rowBaseX = worldCenter.x + xOffset;

            int cMin = Mathf.CeilToInt((viewRect.xMin + half - rowBaseX) / s);
            int cMax = Mathf.FloorToInt((viewRect.xMax - half - rowBaseX) / s);

            for (int c = cMin; c <= cMax; c++)
            {
                Vector2 squareCenter = new Vector2(rowBaseX + c * s, y);
                AddSquare(squareCenter);
            }
        }

        // 三角行：上下各扩一段“虚拟方形行”来源，补齐上下边缘
        int rTriMin = Mathf.CeilToInt((viewRect.yMin - half - worldCenter.y) / rowStep);
        int rTriMax = Mathf.FloorToInt((viewRect.yMax + half - worldCenter.y) / rowStep);

        for (int r = rTriMin; r <= rTriMax; r++)
        {
            float y = worldCenter.y + r * rowStep;
            float xOffset = ((r & 1) != 0) ? (s * 0.5f) : 0f;
            float rowBaseX = worldCenter.x + xOffset;

            int cMin = Mathf.CeilToInt((viewRect.xMin + half - rowBaseX) / s);
            int cMax = Mathf.FloorToInt((viewRect.xMax - half - rowBaseX) / s);

            for (int c = cMin; c <= cMax; c++)
            {
                Vector2 squareCenter = new Vector2(rowBaseX + c * s, y);

                Vector2 upTriCentroid = new Vector2(squareCenter.x, squareCenter.y + half + h / 3f);
                AddTriangle(upTriCentroid, 0f);

                Vector2 downTriCentroid = new Vector2(squareCenter.x, squareCenter.y - half - h / 3f);
                AddTriangle(downTriCentroid, 180f);
            }
        }
    }

    private enum TileType
    {
        Triangle, Square
    }

    protected override Vector2[] GetCellVertices(Cell c)
    {
        if (placedVertices.TryGetValue(c, out var verts))
        {
            return verts;
        }

        float s = cellSize;
        float h = Mathf.Sqrt(3f) * 0.5f * s;
        Quaternion rot = c.transform.rotation;
        Vector2 center = new Vector2(c.transform.position.x, c.transform.position.y);

        // 用池类型判定，避免把 rot=0 的方形误判成三角形
        if (c.pool == PoolManager.instance.triangle)
        {
            Vector2 vTop = new Vector2(0f, 2f * h / 3f);
            Vector2 vBR = new Vector2(s * 0.5f, -h / 3f);
            Vector2 vBL = new Vector2(-s * 0.5f, -h / 3f);
            return new Vector2[]
            {
            center + (Vector2)(rot * vTop),
            center + (Vector2)(rot * vBR),
            center + (Vector2)(rot * vBL)
            };
        }
        else
        {
            float half = s * 0.5f;
            Vector2[] local = new Vector2[]
            {
            new Vector2(-half, -half),
            new Vector2( half, -half),
            new Vector2( half,  half),
            new Vector2(-half,  half)
            };

            Vector2[] outv = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                outv[i] = center + (Vector2)(rot * local[i]);
            }

            return outv;
        }
    }
}