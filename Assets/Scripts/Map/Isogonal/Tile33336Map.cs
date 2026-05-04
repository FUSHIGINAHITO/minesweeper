using UnityEngine;

/// <summary>
/// 阿基米德密铺 3.3.3.3.6
/// </summary>
public class Tile33336Map : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Triangle;
    public override int ShapeNum => 2;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float h = Mathf.Sqrt(3f) * 0.5f * s;

        // 3.3.3.3.6 的六边形中心构成 index-7 子晶格（最小平移基元面积对应 1Hex + 8Tri）
        // UvToWorld(a, b) = ((a + 0.5*b) * s, b * h)
        b1 = new Vector2((2f + 0.5f * 1f) * s, 1f * h);
        b2 = new Vector2((-1f + 0.5f * 3f) * s, 3f * h);

        motif = new[]
        {
            // 1 个六边形
            new MotifCell(CellShapeType.Hex, Vector2.zero, 0f, 0),

            // 4 个上三角（重心: a + 1/3, b + 1/3）
            new MotifCell(CellShapeType.Triangle, new Vector2(((1f + 1f / 3f) + 0.5f * (0f + 1f / 3f)) * s, (0f + 1f / 3f) * h), 0f, 1),
            new MotifCell(CellShapeType.Triangle, new Vector2(((-1f + 1f / 3f) + 0.5f * (-2f + 1f / 3f)) * s, (-2f + 1f / 3f) * h), 0f, 1),
            new MotifCell(CellShapeType.Triangle, new Vector2(((-1f + 1f / 3f) + 0.5f * (1f + 1f / 3f)) * s, (1f + 1f / 3f) * h), 0f, 1),
            new MotifCell(CellShapeType.Triangle, new Vector2(((0f + 1f / 3f) + 0.5f * (1f + 1f / 3f)) * s, (1f + 1f / 3f) * h), 0f, 1),

            // 4 个下三角（重心: a + 2/3, b + 2/3）
            new MotifCell(CellShapeType.Triangle, new Vector2(((0f + 2f / 3f) + 0.5f * (0f + 2f / 3f)) * s, (0f + 2f / 3f) * h), 180f, 1),
            new MotifCell(CellShapeType.Triangle, new Vector2(((-1f + 2f / 3f) + 0.5f * (-2f + 2f / 3f)) * s, (-2f + 2f / 3f) * h), 180f, 1),
            new MotifCell(CellShapeType.Triangle, new Vector2(((-1f + 2f / 3f) + 0.5f * (1f + 2f / 3f)) * s, (1f + 2f / 3f) * h), 180f, 1),
            new MotifCell(CellShapeType.Triangle, new Vector2(((0f + 2f / 3f) + 0.5f * (1f + 2f / 3f)) * s, (1f + 2f / 3f) * h), 180f, 1)
        };
    }
}