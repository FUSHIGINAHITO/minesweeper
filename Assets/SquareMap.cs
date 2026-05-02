using UnityEngine;

public class SquareMap : Map
{
    private int gridWidth;
    private int gridHeight;

    protected override void GenerateGrid()
    {
        gridWidth = Mathf.Max(1, Mathf.FloorToInt(worldWidth / cellSize));
        gridHeight = Mathf.Max(1, Mathf.FloorToInt(worldHeight / cellSize));

        var centerWorld = (bottomLeft + topRight) * 0.5f;
        var origin = new Vector3(
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

                var cell = PoolManager.instance.square.Require();
                cell.transform.SetParent(transform);
                cell.transform.position = position;
                cell.transform.localScale = cellSize * Vector3.one;
                cell.Init();

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