using UnityEngine;

public class SquareMap : Map
{
    private int gridWidth;
    private int gridHeight;

    protected override void GenerateGrid()
    {
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
                var obj = Instantiate(cellPrefab, position, Quaternion.identity, transform);

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