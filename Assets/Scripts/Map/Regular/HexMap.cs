using UnityEngine;

/// <summary>
/// 柏拉图密铺 6.6.6
/// </summary>
public class HexMap : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Hex;
    public override int ShapeNum => 1;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float hexHeight = Mathf.Sqrt(3f) * s;

        // flat-top 六边形中心点晶格基向量
        b1 = new Vector2(1.5f * s, 0.5f * hexHeight);
        b2 = new Vector2(0f, hexHeight);

        motif = new[]
        {
            new MotifCell(CellShapeType.Hex, Vector2.zero, 0f, 0)
        };
    }
}