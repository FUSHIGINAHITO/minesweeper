using UnityEngine;

/// <summary>
/// 阿基米德密铺 3.3.4.3.4
/// </summary>
public class Tile33434Map : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Triangle;
    public override int ShapeNum => 2;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float sqrt3 = Mathf.Sqrt(3f);
        float half = s * 0.5f;
        float h = sqrt3 * 0.5f * s;

        float dEdge = half + h / 3f;
        float dCornerTri = half + 2f * h / 3f;

        float cx = (3f + sqrt3) * 0.25f * s;
        float cy = (1f + sqrt3) * 0.25f * s;

        float a = (1f + sqrt3 * 0.5f) * s;

        b1 = new Vector2(a, half);
        b2 = new Vector2(-half, a);

        motif = new[]
        {
            new MotifCell(CellShapeType.Square, Vector2.zero, 0f, 0),
            new MotifCell(CellShapeType.Square, new Vector2(cx, -cy), 30f, 0),

            new MotifCell(CellShapeType.Triangle, new Vector2(0f, dEdge), 0f, 1),
            new MotifCell(CellShapeType.Triangle, new Vector2(dEdge, 0f), -90f, 1),
            new MotifCell(CellShapeType.Triangle, new Vector2(0f, -dEdge), 180f, 1),
            new MotifCell(CellShapeType.Triangle, new Vector2(-dEdge, 0f), 90f, 1),
        };
    }
}