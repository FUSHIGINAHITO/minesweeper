using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 阿基米德密铺 3.12.12
/// </summary>
public class Tile31212Map : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Triangle;
    public override int ShapeNum => 2;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float r12 = s / (2f * Mathf.Tan(Mathf.PI / 12f));
        float rTri = s * Mathf.Sqrt(3f) / 6f;

        // 十二边形中心构成三角晶格，最近中心距
        float D = r12 * 2f;
        // 三角形位于三个十二边形之间（角方向）
        float dCorner = r12 + rTri;

        b1 = new Vector2(D, 0f);
        b2 = new Vector2(D * 0.5f, D * Mathf.Sqrt(3f) * 0.5f);

        var list = new List<MotifCell>(7)
        {
            new(CellShapeType.Dodeca, Vector2.zero, 0f, 0)
        };

        for (int k = 0; k < 2; k++)
        {
            float deg = 30f + k * 60f;
            float a = deg * Mathf.Deg2Rad;
            Vector2 c = new(Mathf.Cos(a) * dCorner, Mathf.Sin(a) * dCorner);
            list.Add(new MotifCell(CellShapeType.Triangle, c, deg - 90f, 1));
        }

        motif = list.ToArray();
    }
}