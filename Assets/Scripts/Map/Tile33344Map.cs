using UnityEngine;

/// <summary>
/// °¢»ùÃ×µÂÃÜÆÌ 3.3.3.4.4
/// </summary>
public class Tile33344Map : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Triangle;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float half = s * 0.5f;
        float h = Mathf.Sqrt(3f) * 0.5f * s;
        float rowStep = s + h;

        b1 = new Vector2(s, 0f);
        b2 = new Vector2(0.5f * s, rowStep);

        motif = new[]
        {
            new MotifCell(CellShapeType.Square, Vector2.zero, 0f),
            new MotifCell(CellShapeType.Triangle, new Vector2(0f, half + h / 3f), 0f),
            new MotifCell(CellShapeType.Triangle, new Vector2(0f, -half - h / 3f), 180f)
        };
    }
}