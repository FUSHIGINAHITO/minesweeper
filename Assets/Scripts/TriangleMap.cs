using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class TriangleMap : TilingMap
{
    protected override void GenerateGrid()
    {
        float s = cellSize;
        float h = Mathf.Sqrt(3f) * 0.5f * s;

        int n = 0;
        for (int tryN = 1; tryN < 500; tryN++)
        {
            float L = tryN * s;
            float H = tryN * h;
            if (L <= worldWidth + 1e-6f && H <= worldHeight + 1e-6f)
            {
                n = tryN;
            }
            else
            {
                break;
            }
        }

        if (n < 1)
        {
            n = 1;
        }

        var nodePositions = new List<Vector3>();
        var nodeIndex = new Dictionary<(int t, int u), int>();

        for (int t = 0; t <= n; t++)
        {
            for (int u = 0; u <= t; u++)
            {
                float x = (u - t * 0.5f) * s;
                float y = -t * h;
                var pos = new Vector3(x, y, 0f);
                nodeIndex[(t, u)] = nodePositions.Count;
                nodePositions.Add(pos);
            }
        }

        var trianglesNodes = new List<int[]>();
        var triangleUp = new List<bool>();

        for (int rNode = 0; rNode < n; rNode++)
        {
            for (int u = 0; u <= rNode; u++)
            {
                int a = nodeIndex[(rNode, u)];
                int b = nodeIndex[(rNode + 1, u)];
                int c = nodeIndex[(rNode + 1, u + 1)];
                trianglesNodes.Add(new[] { a, b, c });
                triangleUp.Add(true);
            }

            for (int u = 0; u <= rNode - 1; u++)
            {
                int a = nodeIndex[(rNode, u)];
                int b = nodeIndex[(rNode, u + 1)];
                int c = nodeIndex[(rNode + 1, u + 1)];
                trianglesNodes.Add(new[] { a, b, c });
                triangleUp.Add(false);
            }
        }

        var centers = new List<Vector3>(trianglesNodes.Count);
        for (int tIdx = 0; tIdx < trianglesNodes.Count; tIdx++)
        {
            var tri = trianglesNodes[tIdx];
            Vector3 c = (nodePositions[tri[0]] + nodePositions[tri[1]] + nodePositions[tri[2]]) / 3f;
            centers.Add(c);
        }

        if (centers.Count == 0)
        {
            centers.Add(Vector3.zero);
            triangleUp.Add(true);
        }

        Vector3 minPos = centers[0];
        Vector3 maxPos = centers[0];
        foreach (var p in centers)
        {
            minPos = Vector3.Min(minPos, p);
            maxPos = Vector3.Max(maxPos, p);
        }

        Vector3 gridCenter = (minPos + maxPos) * 0.5f;
        var availableCenter = (bottomLeft + topRight) * 0.5f;
        Vector3 offset = availableCenter - gridCenter;

        for (int tIdx = 0; tIdx < centers.Count; tIdx++)
        {
            Vector3 worldPos = centers[tIdx] + offset;
            float rotZ = triangleUp[tIdx] ? 0f : 180f;

            var cell = PoolManager.instance.RequireCell(CellShapeType.Triangle, worldPos, Quaternion.Euler(0f, 0f, rotZ), cellSize);
            cellList.Add(cell);
        }
    }
}