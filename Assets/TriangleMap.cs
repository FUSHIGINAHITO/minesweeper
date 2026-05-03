using System.Collections.Generic;
using UnityEngine;

public class TriangleMap : TilingMap
{
    // 每个小三角形的边长被视为 cellSize（用于网格布局计算）
    protected override void GenerateGrid()
    {
        // cellSize 视为小等边三角形的边长 s（用于布局计算）
        float s = cellSize;
        float h = Mathf.Sqrt(3f) * 0.5f * s; // 小三角形高度

        // 选择最大的 n，使得整体大三角形的边长 L = n * s 和高度 H = n * h 都能放下
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
            n = 1;

        // 构建网格节点（用于精确定位三角形重心）
        var nodePositions = new List<Vector3>();
        var nodeIndex = new Dictionary<(int t, int u), int>();

        for (int t = 0; t <= n; t++)
        {
            for (int u = 0; u <= t; u++)
            {
                // x: 居中排列，水平间距为 s；y: 每行下降 h
                float x = (u - t * 0.5f) * s;
                float y = -t * h;
                var pos = new Vector3(x, y, 0f);
                nodeIndex[(t, u)] = nodePositions.Count;
                nodePositions.Add(pos);
            }
        }

        // 构建小三角形（按行），并记录它们使用的节点索引与方向（true = 向上, false = 向下）
        var trianglesNodes = new List<int[]>(); // 每个三角形的三个节点索引
        var triangleUp = new List<bool>(); // 方向标记

        for (int rNode = 0; rNode < n; rNode++)
        {
            int indexInRow = 0;
            // 向上三角形（count = rNode + 1）
            for (int u = 0; u <= rNode; u++)
            {
                int a = nodeIndex[(rNode, u)];
                int b = nodeIndex[(rNode + 1, u)];
                int c = nodeIndex[(rNode + 1, u + 1)];
                trianglesNodes.Add(new int[] { a, b, c });
                triangleUp.Add(true);
                indexInRow++;
            }

            // 向下三角形（count = rNode）， 使用节点 (r, u), (r, u+1), (r+1, u+1)
            for (int u = 0; u <= rNode - 1; u++)
            {
                int a = nodeIndex[(rNode, u)];
                int b = nodeIndex[(rNode, u + 1)];
                int c = nodeIndex[(rNode + 1, u + 1)];
                trianglesNodes.Add(new int[] { a, b, c });
                triangleUp.Add(false);
                indexInRow++;
            }
        }

        // 计算所有三角形的中心点（用于实例化）并记录边界以便居中
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
            trianglesNodes.Add(new int[] { 0, 0, 0 });
            triangleUp.Add(true);
        }

        // 计算相对边界与中心偏移
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

        // 实例化并记录映射：triangle index -> Cell（本地变量即可）
        var triangleToCell = new Cell[trianglesNodes.Count];

        for (int tIdx = 0; tIdx < centers.Count; tIdx++)
        {
            // worldPos 是目标三角形重心在世界坐标下的位置（已居中到可用区域）
            Vector3 worldPos = centers[tIdx] + offset;

            float rotZ = triangleUp[tIdx] ? 0f : 180f;
            Vector3 applyPos = worldPos;

            var cell = PoolManager.instance.triangle.Require();
            cell.Init(applyPos, Quaternion.Euler(0f, 0f, rotZ), cellSize);

            triangleToCell[tIdx] = cell;
            cellList.Add(cell);
        }
    }

    // 返回该 cell 的三个顶点（世界坐标）
    protected override Vector2[] GetCellVertices(Cell c)
    {
        // 边长 s，三角形高度 h
        float s = cellSize;
        float h = Mathf.Sqrt(3f) * 0.5f * s;

        // 相对于三角形重心的顶点（向上三角形的局部坐标）
        // 顶点到重心的距离：顶点方向为 +y: 2h/3；底部顶点在 y = -h/3
        Vector2 vTop = new Vector2(0f, 2f * h / 3f);
        Vector2 vBL = new Vector2(-s * 0.5f, -h / 3f);
        Vector2 vBR = new Vector2(s * 0.5f, -h / 3f);

        // 将局部顶点旋转并平移到世界坐标（使用 transform.rotation）
        Quaternion rot = c.transform.rotation;
        Vector2 center = new Vector2(c.transform.position.x, c.transform.position.y);

        Vector2 wTop = center + (Vector2)(rot * vTop);
        Vector2 wBL = center + (Vector2)(rot * vBL);
        Vector2 wBR = center + (Vector2)(rot * vBR);

        return new Vector2[] { wTop, wBR, wBL };
    }
}