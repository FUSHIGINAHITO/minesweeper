using UnityEngine;

public class SquareMap : Map
{
    private int gridWidth;
    private int gridHeight;

    protected override void GenerateGrid()
    {
        var cam = Camera.main;
        Vector3 origin = Vector3.zero;

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

        gridWidth = Mathf.Max(1, Mathf.FloorToInt(worldWidth / cellSize));
        gridHeight = Mathf.Max(1, Mathf.FloorToInt(worldHeight / cellSize));

        var centerWorld = (bottomLeft + topRight) * 0.5f;
        origin = new Vector3(
            centerWorld.x - (gridWidth - 1) * cellSize * 0.5f,
            centerWorld.y - (gridHeight - 1) * cellSize * 0.5f,
            0f
        );

        cells = new Cell[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                var position = origin + new Vector3(x * cellSize, y * cellSize, 0f);
                var obj = Instantiate(cellPrefab, position, Quaternion.identity);

                obj.transform.localScale = cellSize * Vector3.one;

                var cell = obj.GetComponent<Cell>();
                cells[x, y] = cell;
                cellList.Add(cell);
                cell.i = x;
                cell.j = y;
            }
        }
    }

    // 为所有格子建立邻居列表
    protected override void BuildNeighbours()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                var cell = cells[x, y];
                cell.neighbours.Clear();

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }

                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight)
                        {
                            cell.neighbours.Add(cells[nx, ny]);
                        }
                    }
                }
            }
        }
    }
}