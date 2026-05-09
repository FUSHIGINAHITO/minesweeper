using System;
using System.Collections.Generic;
using UnityEngine;

public class PeriodicMotifMap : TilingMap
{
    public PeriodicMotifSO motifSO;

    [Header("Gizmos")]
    [SerializeField] private bool drawJudgeScreenRectGizmo = true;
    [SerializeField] private bool drawJudgePlayRectGizmo = true;
    [SerializeField] private Color judgeScreenRectGizmoColor = new(0f, 1f, 1f, 1f);
    [SerializeField] private Color judgePlayRectGizmoColor = new(1f, 0.5f, 0f, 1f);
    [SerializeField] private float judgeRectGizmoZ = 0f;

    private Rect lastScreenRect;
    private Rect lastPlayRect;
    private bool hasLastJudgeRects;

    public override CellShapeType BaselineShape => motifSO.baselineShape;
    public override int ShapeNum => motifSO.shapeNum;

    protected readonly struct MotifCell
    {
        public readonly CellShapeType shapeType;
        public readonly Vector2 localCenter;
        public readonly float localRotationDeg;
        public readonly int typeId;

        public MotifCell(CellShapeType shapeType, Vector2 localCenter, float localRotationDeg, int typeId)
        {
            this.shapeType = shapeType;
            this.localCenter = localCenter;
            this.localRotationDeg = localRotationDeg;
            this.typeId = typeId;
        }
    }

    protected virtual int LatticePadding => 2;
    protected virtual float PlacementQuantizeScale => 10000f;

    // 兼容旧实现：当 motifSO 不可用时，仍可由子类覆盖提供数据
    protected virtual void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        b1 = Vector2.right * s;
        b2 = Vector2.up * s;
        motif = Array.Empty<MotifCell>();
    }

    protected bool TryBuildPatternFromMotifSO(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif)
    {
        if (motifSO == null || motifSO.cells == null || motifSO.cells.Length == 0)
        {
            b1 = default;
            b2 = default;
            motif = Array.Empty<MotifCell>();
            return false;
        }

        b1 = motifSO.basis1Unit * s;
        b2 = motifSO.basis2Unit * s;

        var src = motifSO.cells;
        motif = new MotifCell[src.Length];

        for (int i = 0; i < src.Length; i++)
        {
            var c = src[i];
            motif[i] = new MotifCell(
                c.shapeType,
                c.localCenterUnit * s,
                c.localRotationDeg,
                c.typeId);
        }

        return true;
    }

    protected override void GenerateGrid()
    {
        float s = cellSize;
        Rect playRect = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);

        var cam = UIManager.instance.mainCamera;
        float camDistance = Mathf.Abs(cam.transform.position.z);
        Vector3 screenBL3 = cam.ScreenToWorldPoint(new Vector3(0f, 0f, camDistance));
        Vector3 screenTR3 = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, camDistance));
        Rect screenRect = Rect.MinMaxRect(
            Mathf.Min(screenBL3.x, screenTR3.x),
            Mathf.Min(screenBL3.y, screenTR3.y),
            Mathf.Max(screenBL3.x, screenTR3.x),
            Mathf.Max(screenBL3.y, screenTR3.y));

        lastPlayRect = playRect;
        lastScreenRect = screenRect;
        hasLastJudgeRects = true;

        Vector2 origin = new(
            (bottomLeft.x + topRight.x) * 0.5f,
            (bottomLeft.y + topRight.y) * 0.5f);

        Vector2 b1;
        Vector2 b2;
        MotifCell[] motif;

        if (!TryBuildPatternFromMotifSO(s, out b1, out b2, out motif))
        {
            BuildPattern(s, out b1, out b2, out motif);
        }

        if (motif == null || motif.Length == 0)
        {
            return;
        }

        float det = b1.x * b2.y - b1.y * b2.x;
        if (Mathf.Abs(det) < 1e-8f)
        {
            return;
        }

        Vector2 WorldToLattice(Vector2 worldDelta)
        {
            float i = (worldDelta.x * b2.y - worldDelta.y * b2.x) / det;
            float j = (-worldDelta.x * b1.y + worldDelta.y * b1.x) / det;
            return new Vector2(i, j);
        }

        bool PolygonFullyInsideRect(Vector2[] verts, Rect rect)
        {
            for (int i = 0; i < verts.Length; i++)
            {
                if (!rect.Contains(verts[i]))
                {
                    return false;
                }
            }

            return true;
        }

        bool IsPointInPolygon(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            int n = polygon.Length;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];

                bool intersect = ((a.y > p.y) != (b.y > p.y))
                    && (p.x < (b.x - a.x) * (p.y - a.y) / ((b.y - a.y) + 1e-12f) + a.x);

                if (intersect)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            const float eps = 1e-6f;
            if (Mathf.Abs(Cross(a, b, p)) > eps)
            {
                return false;
            }

            return p.x >= Mathf.Min(a.x, b.x) - eps && p.x <= Mathf.Max(a.x, b.x) + eps
                && p.y >= Mathf.Min(a.y, b.y) - eps && p.y <= Mathf.Max(a.y, b.y) + eps;
        }

        bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float c1 = Cross(a1, a2, b1);
            float c2 = Cross(a1, a2, b2);
            float c3 = Cross(b1, b2, a1);
            float c4 = Cross(b1, b2, a2);

            bool properIntersect = ((c1 > 0f && c2 < 0f) || (c1 < 0f && c2 > 0f))
                                && ((c3 > 0f && c4 < 0f) || (c3 < 0f && c4 > 0f));
            if (properIntersect)
            {
                return true;
            }

            return OnSegment(a1, a2, b1)
                || OnSegment(a1, a2, b2)
                || OnSegment(b1, b2, a1)
                || OnSegment(b1, b2, a2);
        }

        bool PolygonIntersectsRect(Vector2[] verts, Rect rect)
        {
            for (int i = 0; i < verts.Length; i++)
            {
                if (rect.Contains(verts[i]))
                {
                    return true;
                }
            }

            Vector2 r0 = new(rect.xMin, rect.yMin);
            Vector2 r1 = new(rect.xMin, rect.yMax);
            Vector2 r2 = new(rect.xMax, rect.yMax);
            Vector2 r3 = new(rect.xMax, rect.yMin);

            if (IsPointInPolygon(r0, verts)
                || IsPointInPolygon(r1, verts)
                || IsPointInPolygon(r2, verts)
                || IsPointInPolygon(r3, verts))
            {
                return true;
            }

            Vector2[] rectCorners = { r0, r1, r2, r3 };

            for (int i = 0; i < verts.Length; i++)
            {
                Vector2 a = verts[i];
                Vector2 b = verts[(i + 1) % verts.Length];

                for (int e = 0; e < 4; e++)
                {
                    Vector2 c = rectCorners[e];
                    Vector2 d = rectCorners[(e + 1) % 4];

                    if (SegmentsIntersect(a, b, c, d))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        Vector2[] BuildWorldVertices(CellShapeType shapeType, Vector2 center, float rotDeg)
        {
            var tile = Game.instance.so.GetTileSO(shapeType);
            var localVerts = tile.localVertices;
            int count = localVerts.Length;
            var worldVerts = new Vector2[count];

            float rad = rotDeg * Mathf.Deg2Rad;
            float cr = Mathf.Cos(rad);
            float sr = Mathf.Sin(rad);

            for (int i = 0; i < count; i++)
            {
                Vector2 p = localVerts[i] * s;
                Vector2 r = new(cr * p.x - sr * p.y, sr * p.x + cr * p.y);
                worldVerts[i] = center + r;
            }

            return worldVerts;
        }

        var placedKeys = new HashSet<(long x, long y, CellShapeType t, int r)>();

        void TryAddCell(CellShapeType shapeType, Vector2 center, float rotDeg, int typeId)
        {
            long qx = (long)Mathf.Round(center.x * PlacementQuantizeScale);
            long qy = (long)Mathf.Round(center.y * PlacementQuantizeScale);
            int qr = Mathf.RoundToInt(rotDeg * 100f);
            var key = (qx, qy, shapeType, qr);

            if (placedKeys.Contains(key))
            {
                return;
            }

            Vector2[] verts = BuildWorldVertices(shapeType, center, rotDeg);

            if (PolygonFullyInsideRect(verts, playRect))
            {
                var cell = PoolManager.instance.RequireCell(
                    shapeType,
                    new Vector3(center.x, center.y, 0f),
                    Quaternion.Euler(0f, 0f, rotDeg),
                    s,
                    false,
                    typeId);

                cellList.Add(cell);
                allCellList.Add(cell);
                placedKeys.Add(key);
                return;
            }

            if (!PolygonIntersectsRect(verts, screenRect))
            {
                return;
            }

            var borderCell = PoolManager.instance.RequireCell(
                shapeType,
                new Vector3(center.x, center.y, 0f),
                Quaternion.Euler(0f, 0f, rotDeg),
                s,
                true,
                typeId);

            allCellList.Add(borderCell);
            placedKeys.Add(key);
        }

        Vector2[] corners =
        {
            new(screenRect.xMin, screenRect.yMin),
            new(screenRect.xMin, screenRect.yMax),
            new(screenRect.xMax, screenRect.yMin),
            new(screenRect.xMax, screenRect.yMax)
        };

        float minI = float.PositiveInfinity;
        float maxI = float.NegativeInfinity;
        float minJ = float.PositiveInfinity;
        float maxJ = float.NegativeInfinity;

        for (int k = 0; k < corners.Length; k++)
        {
            Vector2 lattice = WorldToLattice(corners[k] - origin);

            if (lattice.x < minI)
            {
                minI = lattice.x;
            }

            if (lattice.x > maxI)
            {
                maxI = lattice.x;
            }

            if (lattice.y < minJ)
            {
                minJ = lattice.y;
            }

            if (lattice.y > maxJ)
            {
                maxJ = lattice.y;
            }
        }

        int iMin = Mathf.FloorToInt(minI) - LatticePadding;
        int iMax = Mathf.CeilToInt(maxI) + LatticePadding;
        int jMin = Mathf.FloorToInt(minJ) - LatticePadding;
        int jMax = Mathf.CeilToInt(maxJ) + LatticePadding;

        for (int j = jMin; j <= jMax; j++)
        {
            for (int i = iMin; i <= iMax; i++)
            {
                Vector2 latticeBase = origin + i * b1 + j * b2;

                for (int m = 0; m < motif.Length; m++)
                {
                    Vector2 center = latticeBase + motif[m].localCenter;
                    int motifTypeId = motif[m].typeId;
                    TryAddCell(motif[m].shapeType, center, motif[m].localRotationDeg, motifTypeId);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        bool hasPlayRect = false;
        bool hasScreenRect = false;
        Rect playRect = default;
        Rect screenRect = default;

        if (hasLastJudgeRects)
        {
            playRect = lastPlayRect;
            screenRect = lastScreenRect;
            hasPlayRect = true;
            hasScreenRect = true;
        }
        else
        {
            if (topRight.x > bottomLeft.x && topRight.y > bottomLeft.y)
            {
                playRect = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
                hasPlayRect = true;
            }

            if (TryGetCurrentScreenRect(out var currentScreenRect))
            {
                screenRect = currentScreenRect;
                hasScreenRect = true;
            }
        }

        if (drawJudgePlayRectGizmo && hasPlayRect)
        {
            DrawRectGizmo(playRect, judgePlayRectGizmoColor, judgeRectGizmoZ);
        }

        if (drawJudgeScreenRectGizmo && hasScreenRect)
        {
            DrawRectGizmo(screenRect, judgeScreenRectGizmoColor, judgeRectGizmoZ);
        }
    }

    private static void DrawRectGizmo(Rect rect, Color color, float z)
    {
        Gizmos.color = color;

        Vector3 p0 = new(rect.xMin, rect.yMin, z);
        Vector3 p1 = new(rect.xMin, rect.yMax, z);
        Vector3 p2 = new(rect.xMax, rect.yMax, z);
        Vector3 p3 = new(rect.xMax, rect.yMin, z);

        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);
    }

    private static bool TryGetCurrentScreenRect(out Rect screenRect)
    {
        screenRect = default;

        if (UIManager.instance == null || UIManager.instance.mainCamera == null)
        {
            return false;
        }

        var cam = UIManager.instance.mainCamera;
        float camDistance = Mathf.Abs(cam.transform.position.z);

        Vector3 screenBL3 = cam.ScreenToWorldPoint(new Vector3(0f, 0f, camDistance));
        Vector3 screenTR3 = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, camDistance));

        screenRect = Rect.MinMaxRect(
            Mathf.Min(screenBL3.x, screenTR3.x),
            Mathf.Min(screenBL3.y, screenTR3.y),
            Mathf.Max(screenBL3.x, screenTR3.x),
            Mathf.Max(screenBL3.y, screenTR3.y));

        return true;
    }
}