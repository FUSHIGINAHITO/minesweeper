using System;
using System.Collections.Generic;
using UnityEngine;

public class HexMap : TilingMap
{
    private int gridWidth;
    private int gridHeight;
    private int radius;

    // 生成正六边形外轮廓网格（flat-top，轴坐标 axial -> 像素映射）
    protected override void GenerateGrid()
    {
        // 将 cellSize 视为六边形总体宽度（flat-top）：hexWidth = cellSize = 2 * size
        float hexWidth = cellSize;
        float size = hexWidth * 0.5f;                 // distance from center to flat side horizontally (half width)
        float hexHeight = Mathf.Sqrt(3f) * size;     // vertical span per row

        // 选择合适的 radius，使得正六边形外轮廓整体能放进 worldWidth/worldHeight
        int R = 0;
        for (int tryR = 0; tryR < 100; tryR++)
        {
            float w = size * (3f * tryR + 2f);
            float h = hexHeight * (2f * tryR + 1f);
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
        gridWidth = 2 * radius + 1;
        gridHeight = 2 * radius + 1;

        // 先计算所有相对位置并找出边界，以便居中到 available area
        var positions = new List<(int q, int r, Vector3 pos)>();

        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                // axial -> pixel (flat-top)
                float px = size * 1.5f * q;
                float py = hexHeight * (r + q * 0.5f);
                positions.Add((q, r, new Vector3(px, py, 0f)));
            }
        }

        if (positions.Count == 0)
        {
            // 保底：至少一个格子
            positions.Add((0, 0, Vector3.zero));
            radius = 0;
            gridWidth = 1;
            gridHeight = 1;
        }

        // 计算相对边界与中心偏移
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
            int q = item.q;
            int r = item.r;
            Vector3 pos = item.pos + offset;

            var cell = PoolManager.instance.hex.Require();
            cell.Init(pos, Quaternion.identity, cellSize);

            int i = q + radius;
            int j = r + radius;
            cellList.Add(cell);
        }
    }

    // 返回该 cell 的六个顶点（世界坐标，flat-top）
    protected override Vector2[] GetCellVertices(Cell c)
    {
        float size = cellSize * 0.5f;
        float hexHeight = Mathf.Sqrt(3f) * size;
        Vector3 pos3 = c.transform.position;
        Vector2 center = new Vector2(pos3.x, pos3.y);

        return new Vector2[]
        {
            center + new Vector2( size,  0f),
            center + new Vector2( size * 0.5f,  hexHeight * 0.5f),
            center + new Vector2(-size * 0.5f,  hexHeight * 0.5f),
            center + new Vector2(-size,  0f),
            center + new Vector2(-size * 0.5f, -hexHeight * 0.5f),
            center + new Vector2( size * 0.5f, -hexHeight * 0.5f)
        };
    }
}