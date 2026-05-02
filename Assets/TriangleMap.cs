using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class TriangleMap : Map
{
    private int gridWidth;
    private int gridHeight;

    // 控制邻居判定方式：false = 共享顶点为邻居（原行为），true = 共享边为邻居
    public bool useSharedEdges = false;

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

        if (n < 1) n = 1;

        // 构建网格节点（用于精确共享顶点判定）
        // 节点行 t = 0..n，行 t 有 t+1 个节点
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
        var triangleRowIndex = new List<(int row, int indexInRow)>(); // 用于 cells[,] 映射
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
                triangleRowIndex.Add((rNode, indexInRow));
                triangleUp.Add(true);
                indexInRow++;
            }

            // 向下三角形（count = rNode）， 修正：使用节点 (r, u), (r, u+1), (r+1, u+1)
            for (int u = 0; u <= rNode - 1; u++)
            {
                int a = nodeIndex[(rNode, u)];
                int b = nodeIndex[(rNode, u + 1)];
                int c = nodeIndex[(rNode + 1, u + 1)];
                trianglesNodes.Add(new int[] { a, b, c });
                triangleRowIndex.Add((rNode, indexInRow));
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
            triangleRowIndex.Add((0, 0));
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

        // 分配 cells[,]，我们使用 gridWidth = n, gridHeight = 2*n 以容纳每行最多 2*row+1 个三角形（最大 < 2*n）
        gridWidth = Mathf.Max(1, n);
        gridHeight = Mathf.Max(1, 2 * n);
        cells = new Cell[gridWidth, gridHeight];

        // 实例化并记录映射：triangle index -> Cell
        var triangleToCell = new Cell[trianglesNodes.Count];

        for (int tIdx = 0; tIdx < centers.Count; tIdx++)
        {
            // worldPos 是目标三角形重心在世界坐标下的位置（已居中到可用区域）
            Vector3 worldPos = centers[tIdx] + offset;

            float rotZ = triangleUp[tIdx] ? 0f : 180f;
            // 将用户可调偏移应用到最终位置（世界单位）
            Vector3 applyPos = worldPos;

            var obj = PoolManager.instance.triangle.Require();
            obj.transform.SetParent(transform);
            obj.transform.SetPositionAndRotation(applyPos, Quaternion.Euler(0f, 0f, rotZ));
            obj.transform.localScale = cellSize * Vector3.one;
            var cell = obj.GetComponent<Cell>();
            cell.Init();

            triangleToCell[tIdx] = cell;
            cellList.Add(cell);

            var (row, idxInRow) = triangleRowIndex[tIdx];
            int i = row;
            int j = idxInRow;
            // bounds check (should be safe)
            if (i >= 0 && i < gridWidth && j >= 0 && j < gridHeight)
            {
                cells[i, j] = cell;
            }

            cell.i = i;
            cell.j = j;
        }

        // 保存节点和三角形拓扑信息到字段（在 BuildNeighbours 中使用）
        this._nodePositions = nodePositions;
        this._trianglesNodes = trianglesNodes;
        this._triangleToCell = triangleToCell;
        this._triangleUp = triangleUp;
        this._centers = centers;
    }

    // 存储用于 BuildNeighbours 的中间结构
    private List<Vector3> _nodePositions = new();
    private List<int[]> _trianglesNodes = new();
    private Cell[] _triangleToCell = new Cell[0];
    private List<bool> _triangleUp = new();
    private List<Vector3> _centers = new();

    // 建立邻居：根据 useSharedEdges 决定使用共享顶点还是共享边作为邻居判定
    protected override void BuildNeighbours()
    {
        // 清空所有 cell 的 neighbours
        foreach (var c in cellList)
        {
            c.neighbours.Clear();
        }

        int triCount = _trianglesNodes.Count;
        if (triCount == 0) return;

        if (!useSharedEdges)
        {
            // 原有行为：共享顶点为邻居
            // 构建节点 -> 三角形列表映射
            var nodeToTriangles = new Dictionary<int, List<int>>();
            for (int t = 0; t < triCount; t++)
            {
                var tri = _trianglesNodes[t];
                foreach (var nodeIdx in tri)
                {
                    if (!nodeToTriangles.TryGetValue(nodeIdx, out var list))
                    {
                        list = new List<int>();
                        nodeToTriangles[nodeIdx] = list;
                    }
                    list.Add(t);
                }
            }

            // 对每个三角形，邻居集合为所有共享任一节点的三角形（去重并排除自身）
            for (int t = 0; t < triCount; t++)
            {
                var cell = _triangleToCell[t];
                if (cell == null) continue;

                var neighboursSet = new HashSet<Cell>();
                var tri = _trianglesNodes[t];
                foreach (var nodeIdx in tri)
                {
                    if (nodeToTriangles.TryGetValue(nodeIdx, out var linkedTris))
                    {
                        foreach (var lt in linkedTris)
                        {
                            if (lt == t) continue;
                            var nc = _triangleToCell[lt];
                            if (nc != null)
                            {
                                neighboursSet.Add(nc);
                            }
                        }
                    }
                }

                foreach (var nc in neighboursSet)
                {
                    cell.neighbours.Add(nc);
                }
            }
        }
        else
        {
            // 新行为：共享边为邻居
            // 构建边（无向） -> 三角形列表映射，边以 (min,max) 的节点索引对表示
            var edgeToTriangles = new Dictionary<(int a, int b), List<int>>();
            for (int t = 0; t < triCount; t++)
            {
                var tri = _trianglesNodes[t];
                // 三条边
                var edges = new (int, int)[]
                {
                    (tri[0], tri[1]),
                    (tri[1], tri[2]),
                    (tri[2], tri[0])
                };

                foreach (var e in edges)
                {
                    int a = e.Item1, b = e.Item2;
                    if (a > b) (a, b) = (b, a);
                    var key = (a, b);
                    if (!edgeToTriangles.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        edgeToTriangles[key] = list;
                    }
                    list.Add(t);
                }
            }

            // 对每个三角形，邻居集合为所有共享任一边的三角形（去重并排除自身）
            for (int t = 0; t < triCount; t++)
            {
                var cell = _triangleToCell[t];
                if (cell == null) continue;

                var neighboursSet = new HashSet<Cell>();
                var tri = _trianglesNodes[t];
                var edges = new (int, int)[]
                {
                    (tri[0], tri[1]),
                    (tri[1], tri[2]),
                    (tri[2], tri[0])
                };

                foreach (var e in edges)
                {
                    int a = e.Item1, b = e.Item2;
                    if (a > b) (a, b) = (b, a);
                    var key = (a, b);
                    if (edgeToTriangles.TryGetValue(key, out var linkedTris))
                    {
                        foreach (var lt in linkedTris)
                        {
                            if (lt == t) continue;
                            var nc = _triangleToCell[lt];
                            if (nc != null)
                            {
                                neighboursSet.Add(nc);
                            }
                        }
                    }
                }

                foreach (var nc in neighboursSet)
                {
                    cell.neighbours.Add(nc);
                }
            }
        }
    }
}