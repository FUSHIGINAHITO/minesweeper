using UnityEngine;

public partial class PolygonPlacer : MonoBehaviour
{
    [Header("Gizmos")]
    [SerializeField] private bool drawSnapGizmos = true;
    [SerializeField] private Color handSnapEdgeGizmoColor = Color.cyan;
    [SerializeField] private Color handOtherEdgesGizmoColor = new Color(0f, 1f, 1f, 0.35f);
    [SerializeField] private Color boundarySnapEdgeGizmoColor = Color.yellow;
    [SerializeField] private Color boundaryOtherEdgesGizmoColor = new Color(1f, 0.92f, 0.016f, 0.35f);
    [SerializeField] private Color handNormalGizmoColor = Color.magenta;
    [SerializeField] private Color boundaryNormalGizmoColor = new Color(1f, 0.5f, 0f, 1f);
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

        // 1) 始终绘制场上边界的所有边
        Gizmos.color = boundaryOtherEdgesGizmoColor;
        for (int i = 0; i < boundaryEdges.Count; i++)
        {
            BoundaryEdge e = boundaryEdges[i];
            Vector3 a = new Vector3(e.a.x, e.a.y, placementZ);
            Vector3 b = new Vector3(e.b.x, e.b.y, placementZ);
            Gizmos.DrawLine(a, b);
        }

        // 2) 始终绘制手上多边形的所有边（有选中 tile 时）
        bool hasValidTile =
            currentTileIndex >= 0 &&
            currentTileIndex < mainDataSO.tiles.Count &&
            mainDataSO.tiles[currentTileIndex] != null &&
            mainDataSO.tiles[currentTileIndex].localVertices != null &&
            mainDataSO.tiles[currentTileIndex].localVertices.Length >= 2;

        Vector2 handA2 = default;
        Vector2 handB2 = default;
        bool hasValidHandSnapEdge = false;

        if (hasValidTile)
        {
            TileSO tile = mainDataSO.tiles[currentTileIndex];
            int edgeCount = tile.localVertices.Length;

            Vector3[] handVerts = new Vector3[edgeCount];
            for (int i = 0; i < edgeCount; i++)
            {
                Vector2 p2 = PolygonSnapSolver.Rotate(tile.localVertices[i] * cellScale, heldRotationDeg) + currentMouseWorld2D;
                handVerts[i] = new Vector3(p2.x, p2.y, placementZ);
            }

            Gizmos.color = handOtherEdgesGizmoColor;
            for (int i = 0; i < edgeCount; i++)
            {
                int next = (i + 1) % edgeCount;
                Gizmos.DrawLine(handVerts[i], handVerts[next]);
            }

            if (selectedSnapEdgeIndex >= 0)
            {
                int edgeIndex = selectedSnapEdgeIndex % edgeCount;
                if (edgeIndex < 0)
                {
                    edgeIndex += edgeCount;
                }

                handA2 = new Vector2(handVerts[edgeIndex].x, handVerts[edgeIndex].y);
                handB2 = new Vector2(handVerts[(edgeIndex + 1) % edgeCount].x, handVerts[(edgeIndex + 1) % edgeCount].y);
                hasValidHandSnapEdge = true;
            }
        }

        // 3) 有吸附时：高亮吸附边 + 端点小球 + 法线
        bool showSnapDetails = hasActiveBoundaryEdge && hasSnapSolution && hasValidHandSnapEdge;
        if (!showSnapDetails)
        {
            return;
        }

        Vector3 handA = new Vector3(handA2.x, handA2.y, placementZ);
        Vector3 handB = new Vector3(handB2.x, handB2.y, placementZ);

        Gizmos.color = handSnapEdgeGizmoColor;
        Gizmos.DrawLine(handA, handB);
        Gizmos.DrawSphere(handA, snapEdgePointGizmoRadius);
        Gizmos.DrawSphere(handB, snapEdgePointGizmoRadius);

        Vector2 boundaryA2 = activeBoundaryEdge.a;
        Vector2 boundaryB2 = activeBoundaryEdge.b;
        Vector3 boundaryA = new Vector3(boundaryA2.x, boundaryA2.y, placementZ);
        Vector3 boundaryB = new Vector3(boundaryB2.x, boundaryB2.y, placementZ);

        Gizmos.color = boundarySnapEdgeGizmoColor;
        Gizmos.DrawLine(boundaryA, boundaryB);
        Gizmos.DrawSphere(boundaryA, snapEdgePointGizmoRadius);
        Gizmos.DrawSphere(boundaryB, snapEdgePointGizmoRadius);

        Vector2 curDir = (handB2 - handA2).normalized;
        if (curDir.sqrMagnitude >= 1e-12f)
        {
            Vector2 curNormal = new Vector2(-curDir.y, curDir.x);
            Vector2 handMid2 = 0.5f * (handA2 + handB2);
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