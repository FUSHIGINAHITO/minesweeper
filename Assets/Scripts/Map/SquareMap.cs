using UnityEngine;

public class SquareMap : TilingMap
{
    private int gridWidth;
    private int gridHeight;

    public override CellShapeType BaselineShape => CellShapeType.Square;

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

                var cell = PoolManager.instance.RequireCell(CellShapeType.Square, position, Quaternion.identity, cellSize);
                cellList.Add(cell);
            }
        }
    }
}