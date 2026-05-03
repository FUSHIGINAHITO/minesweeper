using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// °¢»ùÃ×µÂÃÜÆÌ 3.3.3.4.4
/// </summary>
public class Tile33344Map : TilingMap
{
    public override CellShapeType BaselineShape => CellShapeType.Triangle;

    protected override void GenerateGrid()
    {
        float s = cellSize;
        float half = s * 0.5f;
        float h = Mathf.Sqrt(3f) * 0.5f * s;
        float rowStep = s + h;

        Vector2[] protoTri =
        {
            new(0f, 2f * h / 3f),
            new(0.5f * s, -h / 3f),
            new(-0.5f * s, -h / 3f)
        };

        Vector2[] protoSquare =
        {
            new(-half, -half),
            new(half, -half),
            new(half, half),
            new(-half, half)
        };

        Rect viewRect = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);

        Vector2 worldCenter = new(
            (bottomLeft.x + topRight.x) * 0.5f,
            (bottomLeft.y + topRight.y) * 0.5f);

        var placedKeys = new HashSet<(long x, long y, CellShapeType t)>();

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
            var key = (qx, qy, CellShapeType.Square);
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

            var cell = PoolManager.instance.RequireCell(CellShapeType.Square, new Vector3(center.x, center.y, 0f), Quaternion.identity, s);
            cellList.Add(cell);

            placedKeys.Add(key);
        }

        void AddTriangle(Vector2 centroid, float rotDeg)
        {
            long qx = (long)Mathf.Round(centroid.x * 10000f);
            long qy = (long)Mathf.Round(centroid.y * 10000f);
            var key = (qx, qy, CellShapeType.Triangle);
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
                Vector2 r = new(cr * p.x - sr * p.y, sr * p.x + cr * p.y);
                verts[i] = centroid + r;
            }

            if (!PolygonFullyInsideRect(verts, viewRect))
            {
                return;
            }

            var cell = PoolManager.instance.RequireCell(CellShapeType.Triangle, new Vector3(centroid.x, centroid.y, 0f), Quaternion.Euler(0f, 0f, rotDeg), s);
            cellList.Add(cell);
            placedKeys.Add(key);
        }

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
                Vector2 squareCenter = new(rowBaseX + c * s, y);
                AddSquare(squareCenter);
            }
        }

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
                Vector2 squareCenter = new(rowBaseX + c * s, y);

                Vector2 upTriCentroid = new(squareCenter.x, squareCenter.y + half + h / 3f);
                AddTriangle(upTriCentroid, 0f);

                Vector2 downTriCentroid = new(squareCenter.x, squareCenter.y - half - h / 3f);
                AddTriangle(downTriCentroid, 180f);
            }
        }
    }
}