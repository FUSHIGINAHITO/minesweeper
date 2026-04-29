using System.Collections.Generic;
using UnityEngine;

public class HexMap : Map
{
    private int gridWidth;
    private int gridHeight;
    private int radius;

    // 生成正六边形外轮廓网格（flat-top，轴坐标 axial -> 像素映射）
    protected override void GenerateGrid()
    {
        var cam = Camera.main;
        if (cam == null) return;

        float camDistance = Mathf.Abs(cam.transform.position.z);

        float ml = Mathf.Clamp01(marginLeftPercent);
        float mr = Mathf.Clamp01(marginRightPercent);
        float mt = Mathf.Clamp01(marginTopPercent);
        float mb = Mathf.Clamp01(marginBottomPercent);

        if (ml + mr >= 0.99f)
        {
            float excess = (ml + mr - 0.99f) * 0.5f;
            ml = Mathf.Max(0f, ml - excess);
            mr = Mathf.Max(0f, mr - excess);
        }

        if (mt + mb >= 0.99f)
        {
            float excess = (mt + mb - 0.99f) * 0.5f;
            mt = Mathf.Max(0f, mt - excess);
            mb = Mathf.Max(0f, mb - excess);
        }

        var bottomLeft = cam.ScreenToWorldPoint(new Vector3(Screen.width * ml, Screen.height * mb, camDistance));
        var topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width * (1f - mr), Screen.height * (1f - mt), camDistance));

        float worldWidth = topRight.x - bottomLeft.x;
        float worldHeight = topRight.y - bottomLeft.y;

        // 将 cellSize 视为六边形总体宽度（flat-top）：hexWidth = cellSize = 2 * size
        float hexWidth = cellSize;
        float size = hexWidth * 0.5f;                 // distance from center to flat side horizontally
        float hexHeight = Mathf.Sqrt(3f) * size;     // vertical span per row
        float colSpacing = 1.5f * size;              // horizontal step between q columns
        float rowSpacing = hexHeight;                // vertical step between base rows

        // 选择合适的 radius，使得正六边形外轮廓整体能放进 worldWidth/worldHeight
        // 对于 radius R:
        // overall width = size * (3R + 2)
        // overall height = hexHeight * (2R + 1)
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

        // 分配数组并实例化；使用索引映射 i = q + radius, j = r + radius
        cells = new Cell[gridWidth, gridHeight];

        foreach (var item in positions)
        {
            int q = item.q;
            int r = item.r;
            Vector3 pos = item.pos + offset;

            var obj = Instantiate(cellPrefab, pos, Quaternion.identity);
            obj.transform.localScale = cellSize * Vector3.one;

            var cell = obj.GetComponent<Cell>();
            int i = q + radius;
            int j = r + radius;
            cells[i, j] = cell;
            cellList.Add(cell);
            cell.i = i;
            cell.j = j;
        }
    }

    // 根据轴坐标偏移建立邻居（仅连通存在的格子）
    protected override void BuildNeighbours()
    {
        // axial 6 个方向（flat-top）
        (int dq, int dr)[] directions = new (int, int)[]
        {
            (1, 0),
            (-1, 0),
            (0, 1),
            (0, -1),
            (1, -1),
            (-1, 1)
        };

        for (int i = 0; i < gridWidth; i++)
        {
            for (int j = 0; j < gridHeight; j++)
            {
                var cell = cells[i, j];
                if (cell == null)
                {
                    continue;
                }

                cell.neighbours.Clear();

                // 转回 axial 坐标
                int q = i - radius;
                int r = j - radius;

                foreach (var d in directions)
                {
                    int nq = q + d.dq;
                    int nr = r + d.dr;
                    int ni = nq + radius;
                    int nj = nr + radius;

                    if (ni >= 0 && ni < gridWidth && nj >= 0 && nj < gridHeight)
                    {
                        var ncell = cells[ni, nj];
                        if (ncell != null)
                        {
                            cell.neighbours.Add(ncell);
                        }
                    }
                }
            }
        }
    }
}