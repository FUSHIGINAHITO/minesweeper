using System.Collections.Generic;
using UnityEngine;

public sealed class PolygonSnapSolver
{
    public bool TryFindNearestSnapPair(
    TileSO tile,
    Vector2 centerPos,
    float detectRotationDeg,
    IReadOnlyList<BoundaryEdge> boundaryEdges,
    float cellScale,
    float maxSnapRotateDeg,
    float nearestNormalTieDeg,
    float snapNormalGapTolerance,
    float snapTangentialGapTolerance,
    out BoundaryEdge bestBoundary,
    out int bestTileEdgeIndex)
    {
        bestBoundary = default;
        bestTileEdgeIndex = -1;

        int n = tile.localVertices.Length;
        float bestScore = float.PositiveInfinity;
        bool found = false;

        for (int j = 0; j < boundaryEdges.Count; j++)
        {
            BoundaryEdge be = boundaryEdges[j];

            Vector2 targetDir = (be.a - be.b).normalized;
            if (targetDir.sqrMagnitude < 1e-12f)
            {
                continue;
            }

            Vector2 targetNormal = new Vector2(-targetDir.y, targetDir.x);

            float minAngle = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                Vector2 pa = GetPreviewEdgeWorldA(tile, centerPos, detectRotationDeg, i, cellScale);
                Vector2 pb = GetPreviewEdgeWorldB(tile, centerPos, detectRotationDeg, i, cellScale);

                Vector2 curDir = (pb - pa).normalized;
                if (curDir.sqrMagnitude < 1e-12f)
                {
                    continue;
                }

                Vector2 curNormal = new Vector2(-curDir.y, curDir.x);
                float normalAngle = Vector2.Angle(curNormal, targetNormal);
                if (normalAngle < minAngle)
                {
                    minAngle = normalAngle;
                }
            }

            if (float.IsPositiveInfinity(minAngle))
            {
                continue;
            }

            float angleUpper = Mathf.Min(maxSnapRotateDeg, minAngle + nearestNormalTieDeg);

            int boundaryBestEdgeIndex = -1;
            float boundaryBestScore = float.PositiveInfinity;

            for (int i = 0; i < n; i++)
            {
                Vector2 pa = GetPreviewEdgeWorldA(tile, centerPos, detectRotationDeg, i, cellScale);
                Vector2 pb = GetPreviewEdgeWorldB(tile, centerPos, detectRotationDeg, i, cellScale);

                Vector2 curDir = (pb - pa).normalized;
                if (curDir.sqrMagnitude < 1e-12f)
                {
                    continue;
                }

                Vector2 curNormal = new Vector2(-curDir.y, curDir.x);
                float normalAngle = Vector2.Angle(curNormal, targetNormal);
                if (normalAngle > angleUpper)
                {
                    continue;
                }

                Vector2 handMid = 0.5f * (pa + pb);
                Vector2 targetMid = 0.5f * (be.a + be.b);
                Vector2 deltaMid = handMid - targetMid;

                float normalGap = Mathf.Abs(Vector2.Dot(deltaMid, targetNormal));
                if (normalGap > snapNormalGapTolerance)
                {
                    continue;
                }

                float tangentialGap = Mathf.Abs(Vector2.Dot(deltaMid, targetDir));
                if (tangentialGap > snapTangentialGapTolerance)
                {
                    continue;
                }

                float score = normalGap * 2f + tangentialGap;
                if (score < boundaryBestScore)
                {
                    boundaryBestScore = score;
                    boundaryBestEdgeIndex = i;
                }
            }

            if (boundaryBestEdgeIndex < 0)
            {
                continue;
            }

            if (boundaryBestScore < bestScore)
            {
                bestScore = boundaryBestScore;
                bestBoundary = be;
                bestTileEdgeIndex = boundaryBestEdgeIndex;
                found = true;
            }
        }

        return found;
    }

    public bool TrySolveSnapPoseForSpecificEdge(
        TileSO tile,
        BoundaryEdge boundaryEdge,
        int tileEdgeIndex,
        float cellScale,
        float edgeSnapTolerance,
        out Vector2 snappedCenter,
        out float snappedRotDeg)
    {
        snappedCenter = default;
        snappedRotDeg = 0f;

        Vector2[] local = tile.localVertices;
        int n = local.Length;

        Vector2 p0 = local[tileEdgeIndex] * cellScale;
        Vector2 p1 = local[(tileEdgeIndex + 1) % n] * cellScale;

        Vector2 localEdge = p1 - p0;
        Vector2 targetEdge = boundaryEdge.a - boundaryEdge.b;

        float lenDiff = Mathf.Abs(localEdge.magnitude - targetEdge.magnitude);
        if (lenDiff > edgeSnapTolerance)
        {
            return false;
        }

        float rotDeg = Vector2.SignedAngle(localEdge, targetEdge);
        Vector2 rp0 = Rotate(p0, rotDeg);
        Vector2 rp1 = Rotate(p1, rotDeg);

        Vector2 t = boundaryEdge.b - rp0;
        float err = (rp1 + t - boundaryEdge.a).magnitude;
        if (err > edgeSnapTolerance)
        {
            return false;
        }

        snappedCenter = t;
        snappedRotDeg = rotDeg;
        return true;
    }

    public Vector2[] BuildWorldVertices(TileSO tile, Vector2 pos, float rotDeg, float cellScale)
    {
        Vector2[] src = tile.localVertices;
        int n = src.Length;
        Vector2[] dst = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            Vector2 p = src[i] * cellScale;
            dst[i] = Rotate(p, rotDeg) + pos;
        }

        return dst;
    }

    public static Vector2 Rotate(Vector2 p, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float cr = Mathf.Cos(rad);
        float sr = Mathf.Sin(rad);
        return new Vector2(cr * p.x - sr * p.y, sr * p.x + cr * p.y);
    }

    private static Vector2 GetPreviewEdgeWorldA(TileSO tile, Vector2 centerPos, float rotDeg, int edgeIndex, float cellScale)
    {
        Vector2 p = tile.localVertices[edgeIndex] * cellScale;
        return Rotate(p, rotDeg) + centerPos;
    }

    private static Vector2 GetPreviewEdgeWorldB(TileSO tile, Vector2 centerPos, float rotDeg, int edgeIndex, float cellScale)
    {
        int n = tile.localVertices.Length;
        Vector2 p = tile.localVertices[(edgeIndex + 1) % n] * cellScale;
        return Rotate(p, rotDeg) + centerPos;
    }
}