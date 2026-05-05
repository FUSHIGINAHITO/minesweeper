using UnityEngine;

/// <summary>
/// 柏拉图密铺 3.3.3.3.3.3
/// </summary>
public class TriangleMap : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Triangle;
    public override int ShapeNum => 1;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float h = Mathf.Sqrt(3f) * 0.5f * s;

        // 等边三角形密铺的晶格基向量
        b1 = new Vector2(s, 0f);
        b2 = new Vector2(0.5f * s, h);

        // 一个上三角 + 一个下三角作为最小 motif
        motif = new[]
        {
            new MotifCell(CellShapeType.Triangle, new Vector2(0f, h / 3f), 0f, 0),
            new MotifCell(CellShapeType.Triangle, new Vector2(0f, -h / 3f), 180f, 0)
        };
    }
}