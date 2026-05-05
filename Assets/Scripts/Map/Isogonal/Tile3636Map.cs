using UnityEngine;

/// <summary>
/// 阿基米德密铺 3.6.3.6
/// </summary>
public class Tile3636Map : PeriodicMotifMap
{
    public override CellShapeType BaselineShape => CellShapeType.Triangle;
    public override int ShapeNum => 2;

    protected override void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        float sqrt3 = Mathf.Sqrt(3f);
        float rHex = s * sqrt3 * 0.5f;
        float rTri = s * sqrt3 / 6f;

        float dEdge = rHex + rTri; // hex center -> adjacent triangle center
        float D = 2f * s;          // nearest hex-center distance in 3.6.3.6

        b1 = new Vector2(D, 0f);
        b2 = new Vector2(D * 0.5f, D * sqrt3 * 0.5f);

        motif = new[]
        {
            new MotifCell(CellShapeType.Hex, Vector2.zero, 0f, 0),

            // triangle normals: 90°, 30°
            new MotifCell(CellShapeType.Triangle, new Vector2(0f, dEdge), 0f, 1),
            new MotifCell(CellShapeType.Triangle, new Vector2(D * 0.5f, dEdge * 0.5f), -60f, 1)
        };
    }
}