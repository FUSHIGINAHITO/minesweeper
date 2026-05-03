using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HexMap : TilingMap
{
    public override CellShapeType BaselineShape => CellShapeType.Hex;

    private int radius;

    protected override void GenerateGrid()
    {
        // 统一语义：cellSize 就是正六边形边长
        float side = cellSize;
        float hexHeight = Mathf.Sqrt(3f) * side; // flat-top 六边形总高

        int R = 0;
        for (int tryR = 0; tryR < 100; tryR++)
        {
            // flat-top 半径为 tryR 的整体包围尺寸
            float w = side * (3f * tryR + 2f);
            float h = hexHeight * (tryR * 2f + 1f);
            if (w <= worldWidth && h <= worldHeight)
            {
                R = tryR;
            }
            else
            {
                break;
            }
        }

        radius = Mathf.Max(0, R);

        var positions = new List<(int q, int r, Vector3 pos)>();
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                // axial -> pixel (flat-top), 以边长为基准
                float px = side * 1.5f * q;
                float py = hexHeight * (r + q * 0.5f);
                positions.Add((q, r, new Vector3(px, py, 0f)));
            }
        }

        if (positions.Count == 0)
        {
            positions.Add((0, 0, Vector3.zero));
            radius = 0;
        }

        Vector3 minPos = positions[0].pos;
        Vector3 maxPos = positions[0].pos;
        foreach (var p in positions)
        {
            minPos = Vector3.Min(minPos, p.pos);
            maxPos = Vector3.Max(maxPos, p.pos);
        }

        Vector3 gridCenter = (minPos + maxPos) * 0.5f;
        var availableCenter = (bottomLeft + topRight) * 0.5f;
        Vector3 offset = availableCenter - gridCenter;

        foreach (var item in positions)
        {
            Vector3 pos = item.pos + offset;

            var cell = PoolManager.instance.RequireCell(CellShapeType.Hex, pos, Quaternion.identity, side);
            cellList.Add(cell);
        }
    }
}