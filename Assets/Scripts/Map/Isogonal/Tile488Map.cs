using UnityEngine;

/// <summary>
/// 阿基米德密铺 4.8.8
/// </summary>
public class Tile488Map : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Square;
    public override int ShapeNum => 2;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float r8 = s / (2f * Mathf.Tan(Mathf.PI / 8f));
        float L = 2f * r8 + s; // 相邻八边形中心间距 = (2 + sqrt(2)) * s

        b1 = new Vector2(L, 0f);
        b2 = new Vector2(0f, L);

        motif = new[]
        {
            new MotifCell(CellShapeType.Octa, Vector2.zero, 0f, 0),
            new MotifCell(CellShapeType.Octa, new Vector2(0.5f * L, 0.5f * L), 0f, 0),
            new MotifCell(CellShapeType.Square, new Vector2(0.5f * L, 0f), 0f, 1),
            new MotifCell(CellShapeType.Square, new Vector2(0f, 0.5f * L), 0f, 1)
        };
    }
}