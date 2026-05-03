using System.Collections.Generic;
using UnityEngine;

public abstract class PeriodicMotifMap : TilingMap
{
    protected readonly struct MotifCell
    {
        public readonly CellShapeType shapeType;
        public readonly Vector2 localCenter;
        public readonly float localRotationDeg;

        public MotifCell(CellShapeType shapeType, Vector2 localCenter, float localRotationDeg)
        {
            this.shapeType = shapeType;
            this.localCenter = localCenter;
            this.localRotationDeg = localRotationDeg;
        }
    }

    protected virtual int LatticePadding => 2;
    protected virtual float PlacementQuantizeScale => 10000f;

    protected abstract void BuildPattern(float s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif);

    protected override void GenerateGrid()
    {
        float s = cellSize;
        Rect viewRect = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);

        Vector2 origin = new(
            (bottomLeft.x + topRight.x) * 0.5f,
            (bottomLeft.y + topRight.y) * 0.5f);

        BuildPattern(s, out Vector2 b1, out Vector2 b2, out MotifCell[] motif);

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

        bool PolygonFullyInsideRect(Vector2[] verts)
        {
            for (int i = 0; i < verts.Length; i++)
            {
                if (!viewRect.Contains(verts[i]))
                {
                    return false;
                }
            }

            return true;
        }

        Vector2[] BuildWorldVertices(CellShapeType shapeType, Vector2 center, float rotDeg)
        {
            var localVerts = PoolManager.instance.GetSharedLocalVertices(shapeType);
            int count = localVerts.Count;
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

        void TryAddCell(CellShapeType shapeType, Vector2 center, float rotDeg)
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
            if (!PolygonFullyInsideRect(verts))
            {
                return;
            }

            var cell = PoolManager.instance.RequireCell(
                shapeType,
                new Vector3(center.x, center.y, 0f),
                Quaternion.Euler(0f, 0f, rotDeg),
                s);

            cellList.Add(cell);
            placedKeys.Add(key);
        }

        Vector2[] corners =
        {
            new(viewRect.xMin, viewRect.yMin),
            new(viewRect.xMin, viewRect.yMax),
            new(viewRect.xMax, viewRect.yMin),
            new(viewRect.xMax, viewRect.yMax)
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
                    TryAddCell(motif[m].shapeType, center, motif[m].localRotationDeg);
                }
            }
        }
    }
}