using UnityEngine;

/// <summary>
/// 阿基米德密铺 4.6.12
/// 最小基元：1×12边形 + 2×6边形 + 3×4边形
/// </summary>
public class Tile4612Map : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Square;
    public override int ShapeNum => 3;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float r12 = s / (2f * Mathf.Tan(Mathf.PI / 12f));
        float r4 = s * 0.5f;

        // 4.6.12 的最小平移周期
        float L = 2f * (r12 + r4);

        b1 = new Vector2(L, 0f);
        b2 = new Vector2(L * 0.5f, L * Mathf.Sqrt(3f) * 0.5f);

        Vector2 b1b2 = b1 + b2;

        motif = new[]
        {
            // 1 × dodec
            new MotifCell(CellShapeType.Dodeca, Vector2.zero, 0f, 0),

            // 2 × hex（分数坐标：1/3,1/3 与 2/3,2/3）
            new MotifCell(CellShapeType.Hex, b1b2 / 3f, 0f, 1),
            new MotifCell(CellShapeType.Hex, b1b2 * (2f / 3f), 0f, 1),

            // 3 × square（分数坐标：1/2,0 ; 0,1/2 ; 1/2,1/2）
            new MotifCell(CellShapeType.Square, b1 * 0.5f, 0f, 2),
            new MotifCell(CellShapeType.Square, b2 * 0.5f, 60f, 2),
            new MotifCell(CellShapeType.Square, b1b2 * 0.5f, 120f, 2)
        };
    }
}