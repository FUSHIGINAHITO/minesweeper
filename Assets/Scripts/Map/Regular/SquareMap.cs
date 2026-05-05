using UnityEngine;

/// <summary>
/// 柏拉图密铺 4.4.4.4
/// </summary>
public class SquareMap : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Square;
    public override int ShapeNum => 1;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        b1 = new Vector2(s, 0f);
        b2 = new Vector2(0f, s);

        motif = new[]
        {
            new MotifCell(CellShapeType.Square, Vector2.zero, 0f, 0)
        };
    }
}