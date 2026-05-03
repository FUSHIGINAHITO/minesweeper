using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 阿基米德密铺 3.3.3.3.6
/// </summary>
public class Tile33336Map : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Triangle;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float h = Mathf.Sqrt(3f) * 0.5f * s;

        // 三角晶格坐标 (a, b) -> 世界坐标
        static Vector2 UvToWorld(float a, float b, float side, float height)
        {
            return new Vector2(
                (a + 0.5f * b) * side,
                b * height);
        }

        // 3.3.3.3.6 的六边形中心构成 index-7 子晶格（最小平移基元面积对应 1Hex + 8Tri）
        b1 = UvToWorld(2f, 1f, s, h);
        b2 = UvToWorld(-1f, 3f, s, h);

        var list = new List<MotifCell>(9)
        {
            // 1 个六边形
            new(CellShapeType.Hex, Vector2.zero, 0f)
        };

        void AddUpTri(int a, int b)
        {
            // 上三角形重心: (a + 1/3, b + 1/3)
            Vector2 c = UvToWorld(a + 1f / 3f, b + 1f / 3f, s, h);
            list.Add(new MotifCell(CellShapeType.Triangle, c, 0f));
        }

        void AddDownTri(int a, int b)
        {
            // 下三角形重心: (a + 2/3, b + 2/3)
            Vector2 c = UvToWorld(a + 2f / 3f, b + 2f / 3f, s, h);
            list.Add(new MotifCell(CellShapeType.Triangle, c, 180f));
        }

        // 8 个三角形（4 上 + 4 下）
        AddUpTri(1, 0);
        AddUpTri(-1, -2);
        AddUpTri(-1, 1);
        AddUpTri(0, 1);

        AddDownTri(0, 0);
        AddDownTri(-1, -2);
        AddDownTri(-1, 1);
        AddDownTri(0, 1);

        motif = list.ToArray();
    }
}