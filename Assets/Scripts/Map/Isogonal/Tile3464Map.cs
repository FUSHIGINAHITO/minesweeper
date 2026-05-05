using UnityEngine;

/// <summary>
/// 阿基米德密铺 3.4.6.4
/// </summary>
public class Tile3464Map : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Triangle;
    public override int ShapeNum => 3;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float sqrt3 = Mathf.Sqrt(3f);

        float rHex = s * sqrt3 * 0.5f;
        float rSq = s * 0.5f;
        float RTri = s / sqrt3; // 三角形外接圆半径

        float dHexSq = rHex + rSq;   // hex 与 square 中心距（共边）
        float dHexTri = s + RTri;    // hex 顶点到 triangle 中心（共顶点）

        float D = dHexSq * 2f;

        // 六边形中心组成三角晶格
        b1 = new Vector2(D, 0f);
        b2 = new Vector2(D * 0.5f, D * sqrt3 * 0.5f);

        float h = dHexSq * sqrt3 * 0.5f;

        motif = new[]
        {
            // 1 hex
            new MotifCell(CellShapeType.Hex, Vector2.zero, 30f, 0),

            // 3 squares
            new MotifCell(CellShapeType.Square, new Vector2(dHexSq, 0f), 0f, 1),
            new MotifCell(CellShapeType.Square, new Vector2(dHexSq * 0.5f, h), 60f, 1),
            new MotifCell(CellShapeType.Square, new Vector2(-dHexSq * 0.5f, h), 120f, 1),

            // 2 triangles（两个平移不等价类）
            new MotifCell(CellShapeType.Triangle, new Vector2(dHexTri * Mathf.Cos(30f * Mathf.Deg2Rad), dHexTri * Mathf.Sin(30f * Mathf.Deg2Rad)), 120f, 2),
            new MotifCell(CellShapeType.Triangle, new Vector2(0f, dHexTri), 180f, 2),
        };
    }
}