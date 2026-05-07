using UnityEngine;

public partial class PolygonPlacer : MonoBehaviour
{
    [Header("Gizmos")]
    [SerializeField] private bool drawSnapGizmos = true;
    [SerializeField] private Color handSnapEdgeGizmoColor = Color.cyan; // 判定边（手上）
    [SerializeField] private Color handOtherEdgesGizmoColor = new Color(0f, 1f, 1f, 0.35f);
    [SerializeField] private Color boundarySnapEdgeGizmoColor = Color.yellow; // 判定边（边界）
    [SerializeField] private Color boundaryOtherEdgesGizmoColor = new Color(1f, 0.92f, 0.016f, 0.35f);
    [SerializeField] private Color handNormalGizmoColor = Color.magenta;
    [SerializeField] private Color boundaryNormalGizmoColor = new Color(1f, 0.5f, 0f, 1f);

    [SerializeField] private Color overlapHandPolygonColor = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private Color overlapPlacedPolygonColor = new Color(1f, 0.4f, 0f, 1f);
    [SerializeField, Min(0.01f)] private float normalGizmoLength = 0.35f;
    [SerializeField, Min(0.001f)] private float snapEdgePointGizmoRadius = 0.03f;

    private void OnDrawGizmos()
    {
        if (!drawSnapGizmos || !Application.isPlaying)
        {
            return;
        }

        if (mainDataSO == null || mainDataSO.tiles == null)
        {
            return;
        }

        boundaryTracker.EnsureBoundaryEdges(mainDataSO, cellScale, edgeQuantizeScale);
        var boundaryEdges = boundaryTracker.BoundaryEdges;

        Gizmos.color = boundaryOtherEdgesGizmoColor;
        for (int i = 0; i < boundaryEdges.Count; i++)
        {
            BoundaryEdge e = boundaryEdges[i];
            Gizmos.DrawLine(new Vector3(e.a.x, e.a.y, placementZ), new Vector3(e.b.x, e.b.y, placementZ));
        }

        bool hasValidTile =
            currentTileIndex >= 0 &&
            currentTileIndex < mainDataSO.tiles.Count &&
            mainDataSO.tiles[currentTileIndex] != null &&
            mainDataSO.tiles[currentTileIndex].localVertices != null &&
            mainDataSO.tiles[currentTileIndex].localVertices.Length >= 2;

        if (!hasValidTile)
        {
            return;
        }

        TileSO tile = mainDataSO.tiles[currentTileIndex];
        int edgeCount = tile.localVertices.Length;

        // 手上判定多边形（固定朝向）
        Vector3[] handJudgeVerts = new Vector3[edgeCount];
        for (int i = 0; i < edgeCount; i++)
        {
            Vector2 p2 = PolygonSnapSolver.Rotate(tile.localVertices[i] * cellScale, heldRotationDeg) + currentMouseWorld2D;
            handJudgeVerts[i] = new Vector3(p2.x, p2.y, placementZ);
        }

        Gizmos.color = handOtherEdgesGizmoColor;
        for (int i = 0; i < edgeCount; i++)
        {
            int next = (i + 1) % edgeCount;
            Gizmos.DrawLine(handJudgeVerts[i], handJudgeVerts[next]);
        }

        if (gizmoHasOverlap && gizmoOverlapHandPolygon != null && gizmoOverlapPlacedPolygon != null)
        {
            DrawPolygonGizmo(gizmoOverlapHandPolygon, overlapHandPolygonColor);
            DrawPolygonGizmo(gizmoOverlapPlacedPolygon, overlapPlacedPolygonColor);
        }

        if (!(hasActiveBoundaryEdge && hasSnapSolution))
        {
            return;
        }

        Vector2 boundaryA2 = activeBoundaryEdge.a;
        Vector2 boundaryB2 = activeBoundaryEdge.b;
        Vector3 boundaryA = new Vector3(boundaryA2.x, boundaryA2.y, placementZ);
        Vector3 boundaryB = new Vector3(boundaryB2.x, boundaryB2.y, placementZ);

        // 1) 判定成功的边：用于画法线（不受 R 影响）
        if (detectedSnapEdgeIndex >= 0)
        {
            int detectIdx = detectedSnapEdgeIndex % edgeCount;
            if (detectIdx < 0)
            {
                detectIdx += edgeCount;
            }

            Vector2 handDetectA2 = new Vector2(handJudgeVerts[detectIdx].x, handJudgeVerts[detectIdx].y);
            Vector2 handDetectB2 = new Vector2(handJudgeVerts[(detectIdx + 1) % edgeCount].x, handJudgeVerts[(detectIdx + 1) % edgeCount].y);

            Gizmos.color = handSnapEdgeGizmoColor;
            Gizmos.DrawLine(new Vector3(handDetectA2.x, handDetectA2.y, placementZ), new Vector3(handDetectB2.x, handDetectB2.y, placementZ));

            Gizmos.color = boundarySnapEdgeGizmoColor;
            Gizmos.DrawLine(boundaryA, boundaryB);

            Vector2 curDir = (handDetectB2 - handDetectA2).normalized;
            if (curDir.sqrMagnitude >= 1e-12f)
            {
                Vector2 curNormal = new Vector2(-curDir.y, curDir.x);
                Vector2 handMid2 = 0.5f * (handDetectA2 + handDetectB2);
                Vector3 handMid = new Vector3(handMid2.x, handMid2.y, placementZ);
                Vector3 handNormalEnd = handMid + new Vector3(curNormal.x, curNormal.y, 0f) * normalGizmoLength;

                Gizmos.color = handNormalGizmoColor;
                Gizmos.DrawLine(handMid, handNormalEnd);
                Gizmos.DrawSphere(handNormalEnd, snapEdgePointGizmoRadius * 0.8f);
            }

            Vector2 targetDir = (boundaryA2 - boundaryB2).normalized;
            if (targetDir.sqrMagnitude >= 1e-12f)
            {
                Vector2 targetNormal = new Vector2(-targetDir.y, targetDir.x);
                Vector2 boundaryMid2 = 0.5f * (boundaryA2 + boundaryB2);
                Vector3 boundaryMid = new Vector3(boundaryMid2.x, boundaryMid2.y, placementZ);
                Vector3 boundaryNormalEnd = boundaryMid + new Vector3(targetNormal.x, targetNormal.y, 0f) * normalGizmoLength;

                Gizmos.color = boundaryNormalGizmoColor;
                Gizmos.DrawLine(boundaryMid, boundaryNormalEnd);
                Gizmos.DrawSphere(boundaryNormalEnd, snapEdgePointGizmoRadius * 0.8f);
            }
        }
    }

    private void DrawPolygonGizmo(Vector2[] poly, Color color)
    {
        if (poly == null || poly.Length < 2)
        {
            return;
        }

        Gizmos.color = color;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a2 = poly[i];
            Vector2 b2 = poly[(i + 1) % poly.Length];
            Vector3 a = new Vector3(a2.x, a2.y, placementZ);
            Vector3 b = new Vector3(b2.x, b2.y, placementZ);
            Gizmos.DrawLine(a, b);
        }
    }
}