using UnityEngine;

public class SquareMap : TilingMap
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

                cellList.Add(cell);
            }
        }
    }

    // 返回正方形 cell 的四个角（世界坐标）
    protected override Vector2[] GetCellVertices(Cell c)
    {
        float half = cellSize * 0.5f;
        Vector3 pos3 = c.transform.position;
        Vector2 center = new Vector2(pos3.x, pos3.y);

        return new Vector2[]
        {
            center + new Vector2(-half, -half),
            center + new Vector2( half, -half),
            center + new Vector2( half,  half),
            center + new Vector2(-half,  half)
        };
    }
}