using System.Collections.Generic;
using UnityEngine;

public sealed class PeriodicMotifDetector
{
    public readonly struct DetectionResult
    {
        public readonly Vector2 origin;
        public readonly Vector2 basis1;
        public readonly Vector2 basis2;
        public readonly List<DetectedCell> motifCells;
        public readonly CellShapeType baselineShape;
        public readonly int shapeNum;

        public DetectionResult(
            Vector2 origin,
            Vector2 basis1,
            Vector2 basis2,
            List<DetectedCell> motifCells,
            CellShapeType baselineShape,
            int shapeNum)
        {
            this.origin = origin;
            this.basis1 = basis1;
            this.basis2 = basis2;
            this.motifCells = motifCells;
            this.baselineShape = baselineShape;
            this.shapeNum = shapeNum;
        }
    }

    public readonly struct DetectedCell
    {
        public readonly CellShapeType shapeType;
        public readonly Vector2 localCenter;
        public readonly float localRotationDeg;
        public readonly int typeId;

        public DetectedCell(CellShapeType shapeType, Vector2 localCenter, float localRotationDeg, int typeId)
        {
            this.shapeType = shapeType;
            this.localCenter = localCenter;
            this.localRotationDeg = localRotationDeg;
            this.typeId = typeId;
        }
    }

    private readonly struct VectorKey
    {
        public readonly long x;
        public readonly long y;

        public VectorKey(Vector2 v, float q)
        {
            x = (long)Mathf.Round(v.x * q);
            y = (long)Mathf.Round(v.y * q);
        }

        public override bool Equals(object obj)
        {
            return obj is VectorKey other && x == other.x && y == other.y;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hx = (int)(x ^ (x >> 32));
                int hy = (int)(y ^ (y >> 32));
                return (hx * 397) ^ hy;
            }
        }
    }

    private readonly struct PointKey
    {
        public readonly long x;
        public readonly long y;

        public PointKey(Vector2 p, float q)
        {
            x = (long)Mathf.Round(p.x * q);
            y = (long)Mathf.Round(p.y * q);
        }

        public override bool Equals(object obj)
        {
            return obj is PointKey other && x == other.x && y == other.y;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hx = (int)(x ^ (x >> 32));
                int hy = (int)(y ^ (y >> 32));
                return (hx * 397) ^ hy;
            }
        }
    }

    private readonly struct EdgeKeyUndirected
    {
        public readonly PointKey a;
        public readonly PointKey b;

        public EdgeKeyUndirected(PointKey p0, PointKey p1)
        {
            bool swap = p0.x > p1.x || (p0.x == p1.x && p0.y > p1.y);
            if (swap)
            {
                a = p1;
                b = p0;
            }
            else
            {
                a = p0;
                b = p1;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKeyUndirected other && a.Equals(other.a) && b.Equals(other.b);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (a.GetHashCode() * 397) ^ b.GetHashCode();
            }
        }
    }

    private struct CellRecord
    {
        public int tileIndex;
        public CellShapeType shapeType;
        public Vector2 pos;
        public float rotDeg;
        public int rotKey;
        public Vector2[] verts;
    }

    private struct BoundaryEdge
    {
        public Vector2 a;
        public Vector2 b;
    }

    private struct EdgeCounter
    {
        public int count;
        public Vector2 a;
        public Vector2 b;
    }

    private struct VertexInstance
    {
        public PointKey key;
        public float angle;
    }

    private sealed class DisjointSet
    {
        private readonly int[] parent;
        private readonly byte[] rank;

        public DisjointSet(int n)
        {
            parent = new int[n];
            rank = new byte[n];

            for (int i = 0; i < n; i++)
            {
                parent[i] = i;
                rank[i] = 0;
            }
        }

        public int Find(int x)
        {
            if (parent[x] != x)
            {
                parent[x] = Find(parent[x]);
            }

            return parent[x];
        }

        public void Union(int a, int b)
        {
            int ra = Find(a);
            int rb = Find(b);
            if (ra == rb)
            {
                return;
            }

            if (rank[ra] < rank[rb])
            {
                parent[ra] = rb;
            }
            else if (rank[ra] > rank[rb])
            {
                parent[rb] = ra;
            }
            else
            {
                parent[rb] = ra;
                rank[ra]++;
            }
        }
    }

    // Edmonds Blossom（一般图最大匹配）
    private sealed class BlossomMatcher
    {
        private readonly int n;
        private readonly List<int>[] graph;

        private readonly int[] match;
        private readonly int[] parent;
        private readonly int[] baseVertex;
        private readonly bool[] used;
        private readonly bool[] blossom;

        private readonly Queue<int> queue = new Queue<int>();

        public BlossomMatcher(List<int>[] graph)
        {
            this.graph = graph;
            n = graph.Length;

            match = new int[n];
            parent = new int[n];
            baseVertex = new int[n];
            used = new bool[n];
            blossom = new bool[n];

            for (int i = 0; i < n; i++)
            {
                match[i] = -1;
                parent[i] = -1;
                baseVertex[i] = i;
            }
        }

        public bool TryGetPerfectMatching(out int[] pairMap)
        {
            pairMap = null;

            if ((n & 1) != 0)
            {
                return false;
            }

            for (int i = 0; i < n; i++)
            {
                if (match[i] != -1)
                {
                    continue;
                }

                int finish = FindAugmentingPath(i);
                if (finish < 0)
                {
                    continue;
                }

                Augment(finish);
            }

            for (int i = 0; i < n; i++)
            {
                if (match[i] < 0)
                {
                    return false;
                }
            }

            pairMap = new int[n];
            for (int i = 0; i < n; i++)
            {
                pairMap[i] = match[i];
            }

            return true;
        }

        private void Augment(int v)
        {
            while (v != -1)
            {
                int pv = parent[v];
                int nv = pv == -1 ? -1 : match[pv];

                match[v] = pv;
                if (pv != -1)
                {
                    match[pv] = v;
                }

                v = nv;
            }
        }

        private int FindAugmentingPath(int root)
        {
            for (int i = 0; i < n; i++)
            {
                used[i] = false;
                parent[i] = -1;
                baseVertex[i] = i;
            }

            queue.Clear();
            queue.Enqueue(root);
            used[root] = true;

            while (queue.Count > 0)
            {
                int v = queue.Dequeue();

                List<int> adj = graph[v];
                for (int k = 0; k < adj.Count; k++)
                {
                    int u = adj[k];

                    if (baseVertex[v] == baseVertex[u] || match[v] == u)
                    {
                        continue;
                    }

                    if (u == root || (match[u] != -1 && parent[match[u]] != -1))
                    {
                        int curBase = Lca(v, u);

                        for (int i = 0; i < n; i++)
                        {
                            blossom[i] = false;
                        }

                        MarkPath(v, curBase, u);
                        MarkPath(u, curBase, v);

                        for (int i = 0; i < n; i++)
                        {
                            if (!blossom[baseVertex[i]])
                            {
                                continue;
                            }

                            baseVertex[i] = curBase;
                            if (!used[i])
                            {
                                used[i] = true;
                                queue.Enqueue(i);
                            }
                        }
                    }
                    else if (parent[u] == -1)
                    {
                        parent[u] = v;

                        if (match[u] == -1)
                        {
                            return u;
                        }

                        int m = match[u];
                        used[m] = true;
                        queue.Enqueue(m);
                    }
                }
            }

            return -1;
        }

        private int Lca(int a, int b)
        {
            var visited = new bool[n];

            while (true)
            {
                a = baseVertex[a];
                visited[a] = true;

                if (match[a] == -1)
                {
                    break;
                }

                a = parent[match[a]];
            }

            while (true)
            {
                b = baseVertex[b];
                if (visited[b])
                {
                    return b;
                }

                if (match[b] == -1)
                {
                    break;
                }

                b = parent[match[b]];
            }

            return -1;
        }

        private void MarkPath(int v, int b, int child)
        {
            while (baseVertex[v] != b)
            {
                blossom[baseVertex[v]] = true;
                blossom[baseVertex[match[v]]] = true;

                parent[v] = child;
                child = match[v];
                v = parent[match[v]];
            }
        }
    }

    public bool TryDetect(
    IReadOnlyList<PlacedTileData> placedTiles,
    MainDataSO mainDataSO,
    float cellScale,
    float positionQuantizeScale,
    float rotationQuantizeScale,
    float minTranslationLength,
    float symmetryScoreThreshold,
    out DetectionResult result,
    out string reason)
    {
        _ = symmetryScoreThreshold;

        result = default;
        reason = string.Empty;

        if (placedTiles == null || placedTiles.Count < 1)
        {
            reason = "NO_SAMPLE|场上没有样本。";
            return false;
        }

        if (cellScale <= 0f)
        {
            reason = "BAD_SCALE|cellScale 必须 > 0。";
            return false;
        }

        List<CellRecord> records = BuildRecords(
            placedTiles,
            mainDataSO,
            cellScale,
            positionQuantizeScale,
            rotationQuantizeScale);

        if (records.Count < 1)
        {
            reason = "NO_VALID_CELL|有效单元不足。";
            return false;
        }

        List<BoundaryEdge> boundary = BuildBoundaryEdges(records, positionQuantizeScale);
        if (boundary.Count < 4 || (boundary.Count & 1) != 0)
        {
            reason = "NO_BOUNDARY|边界边数量不足或为奇数，无法构造严格边配对。";
            return false;
        }

        List<Vector2> candidates = BuildTranslationCandidates(boundary, positionQuantizeScale, minTranslationLength);
        if (candidates.Count < 2)
        {
            reason = "NO_CANDIDATE|未找到可用的边界平移候选。";
            return false;
        }

        Vector2 origin = records[0].pos;
        float areaEps = Mathf.Max(1e-8f, 0.01f * cellScale * cellScale);

        bool hasNonCollinearPair = false;
        bool hasPairingPass = false;
        bool hasAnglePass = false;
        bool hasSelfOverlapBasis = false;

        bool found = false;
        float bestArea = -1f;
        DetectionResult best = default;

        for (int i = 0; i < candidates.Count; i++)
        {
            for (int j = i + 1; j < candidates.Count; j++)
            {
                Vector2 b1 = candidates[i];
                Vector2 b2 = candidates[j];

                float area = Mathf.Abs(Cross(b1, b2));
                if (area <= areaEps)
                {
                    continue;
                }

                hasNonCollinearPair = true;

                // 关键：不再 ReduceBasis，避免自动收缩到原胞
                if (!TryBuildBoundaryPairingStrict(boundary, b1, b2, positionQuantizeScale, out int[] pairMap))
                {
                    continue;
                }

                hasPairingPass = true;

                if (!ValidateVertexAngleSumStrictUnionFind(records, boundary, pairMap, positionQuantizeScale))
                {
                    continue;
                }

                hasAnglePass = true;

                // 关键：如果该基向量会让“场上两块”变成晶格整数等价，则会导致你这种超胞基元回铺重叠
                if (HasLatticeEquivalentPair(records, b1, b2, positionQuantizeScale))
                {
                    hasSelfOverlapBasis = true;
                    continue;
                }

                List<DetectedCell> motif = BuildMotif(records, origin, b1, b2, positionQuantizeScale);
                if (motif.Count == 0)
                {
                    continue;
                }

                ComputeShapeInfo(records, out CellShapeType baselineShape, out int shapeNum);
                var current = new DetectionResult(origin, b1, b2, motif, baselineShape, shapeNum);

                if (!found || area > bestArea)
                {
                    found = true;
                    bestArea = area;
                    best = current;
                }
            }
        }

        if (found)
        {
            result = best;
            return true;
        }

        if (!hasNonCollinearPair)
        {
            reason = "NO_BASIS|候选向量两两共线，无法形成二维晶格。";
            return false;
        }

        if (!hasPairingPass)
        {
            reason = "PAIRING_FAIL|边界边无法在晶格下严格一一反向配对。";
            return false;
        }

        if (!hasAnglePass)
        {
            reason = "ANGLE_FAIL|顶点角和不满足 2π，非二维流形。";
            return false;
        }

        if (hasSelfOverlapBasis)
        {
            reason = "BASIS_TOO_SMALL|检测到基向量过小，会使场上基元在晶格平移下自重合。";
            return false;
        }

        reason = "UNKNOWN_FAIL|未找到满足条件的解。";
        return false;
    }

    private static bool HasLatticeEquivalentPair(List<CellRecord> records, Vector2 b1, Vector2 b2, float posQ)
    {
        float det = Cross(b1, b2);
        if (Mathf.Abs(det) < 1e-10f)
        {
            return true;
        }

        // 系数接近整数的门限：至少不小于 1e-5，并随量化尺度收敛
        float coeffEps = Mathf.Max(1e-5f, 2f / Mathf.Max(1f, posQ));

        for (int i = 0; i < records.Count; i++)
        {
            for (int j = i + 1; j < records.Count; j++)
            {
                Vector2 d = records[j].pos - records[i].pos;
                Vector2 uv = WorldToLattice(d, b1, b2, det);

                float m = Mathf.Round(uv.x);
                float n = Mathf.Round(uv.y);

                if (Mathf.Abs(uv.x - m) > coeffEps || Mathf.Abs(uv.y - n) > coeffEps)
                {
                    continue;
                }

                // 关键：用量化后一致性做最终确认，避免固定 epsilon 误判
                Vector2 latticeShift = m * b1 + n * b2;
                Vector2 qd = QuantizeVec2(d, posQ);
                Vector2 qs = QuantizeVec2(latticeShift, posQ);

                if ((qd - qs).sqrMagnitude <= 1e-8f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<CellRecord> BuildRecords(
        IReadOnlyList<PlacedTileData> placedTiles,
        MainDataSO mainDataSO,
        float cellScale,
        float posQ,
        float rotQ)
    {
        var list = new List<CellRecord>(placedTiles.Count);

        for (int i = 0; i < placedTiles.Count; i++)
        {
            PlacedTileData p = placedTiles[i];
            if (p.tileIndex < 0 || p.tileIndex >= mainDataSO.tiles.Count)
            {
                continue;
            }

            TileSO tile = mainDataSO.tiles[p.tileIndex];
            if (tile == null || tile.localVertices == null || tile.localVertices.Length < 3)
            {
                continue;
            }

            Vector2 qPos = QuantizeVec2(p.position, posQ);
            float qRot = QuantizeFloat(p.rotationDeg, rotQ);
            Vector2[] verts = BuildWorldVertices(tile, qPos, qRot, cellScale);

            list.Add(new CellRecord
            {
                tileIndex = p.tileIndex,
                shapeType = tile.shapeType,
                pos = qPos,
                rotDeg = qRot,
                rotKey = Mathf.RoundToInt(qRot * rotQ),
                verts = verts
            });
        }

        return list;
    }

    private static List<BoundaryEdge> BuildBoundaryEdges(List<CellRecord> records, float posQ)
    {
        var map = new Dictionary<EdgeKeyUndirected, EdgeCounter>();

        for (int i = 0; i < records.Count; i++)
        {
            Vector2[] verts = records[i].verts;
            int n = verts.Length;

            for (int e = 0; e < n; e++)
            {
                Vector2 a = verts[e];
                Vector2 b = verts[(e + 1) % n];

                var key = new EdgeKeyUndirected(new PointKey(a, posQ), new PointKey(b, posQ));

                if (map.TryGetValue(key, out EdgeCounter c))
                {
                    c.count++;
                    map[key] = c;
                }
                else
                {
                    map.Add(key, new EdgeCounter
                    {
                        count = 1,
                        a = a,
                        b = b
                    });
                }
            }
        }

        var boundary = new List<BoundaryEdge>();
        foreach (var kv in map)
        {
            if (kv.Value.count == 1)
            {
                boundary.Add(new BoundaryEdge
                {
                    a = kv.Value.a,
                    b = kv.Value.b
                });
            }
        }

        return boundary;
    }

    private static List<Vector2> BuildTranslationCandidates(
        List<BoundaryEdge> boundary,
        float posQ,
        float minLen)
    {
        var set = new HashSet<VectorKey>();
        var list = new List<Vector2>();
        const float epsDir = 1e-5f;

        for (int i = 0; i < boundary.Count; i++)
        {
            Vector2 eA = boundary[i].a;
            Vector2 eB = boundary[i].b;
            Vector2 eDir = eB - eA;
            float eLen = eDir.magnitude;
            if (eLen <= 1e-8f)
            {
                continue;
            }

            Vector2 eDirN = eDir / eLen;

            for (int j = 0; j < boundary.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                Vector2 fA = boundary[j].a;
                Vector2 fB = boundary[j].b;
                Vector2 fDir = fB - fA;
                float fLen = fDir.magnitude;
                if (fLen <= 1e-8f)
                {
                    continue;
                }

                if (Mathf.Abs(eLen - fLen) > 1e-5f)
                {
                    continue;
                }

                Vector2 fDirN = fDir / fLen;
                if ((eDirN + fDirN).sqrMagnitude > epsDir)
                {
                    continue;
                }

                Vector2 t1 = fB - eA;
                Vector2 t2 = fA - eB;
                if ((t1 - t2).sqrMagnitude > 1e-8f)
                {
                    continue;
                }

                if (t1.magnitude < minLen)
                {
                    continue;
                }

                Vector2 qT = QuantizeVec2(t1, posQ);
                var key = new VectorKey(qT, posQ);
                if (set.Add(key))
                {
                    list.Add(qT);
                }
            }
        }

        return list;
    }

    private static bool TryBuildBoundaryPairingStrict(
        List<BoundaryEdge> boundary,
        Vector2 b1,
        Vector2 b2,
        float posQ,
        out int[] pairMap)
    {
        pairMap = null;

        float det = Cross(b1, b2);
        if (Mathf.Abs(det) < 1e-10f)
        {
            return false;
        }

        int n = boundary.Count;
        if ((n & 1) != 0)
        {
            return false;
        }

        var options = new List<int>[n];
        for (int i = 0; i < n; i++)
        {
            options[i] = new List<int>();
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (IsValidBoundaryPair(boundary[i], boundary[j], b1, b2, det, posQ))
                {
                    options[i].Add(j);
                    options[j].Add(i);
                }
            }
        }

        for (int i = 0; i < n; i++)
        {
            if (options[i].Count == 0)
            {
                return false;
            }
        }

        var matcher = new BlossomMatcher(options);
        return matcher.TryGetPerfectMatching(out pairMap);
    }

    private static bool IsValidBoundaryPair(
        BoundaryEdge e,
        BoundaryEdge f,
        Vector2 b1,
        Vector2 b2,
        float det,
        float posQ)
    {
        const float dirEps = 1e-5f;
        const float transEps = 1e-8f;

        Vector2 ai = e.a;
        Vector2 bi = e.b;
        Vector2 di = bi - ai;
        float li = di.magnitude;
        if (li <= 1e-10f)
        {
            return false;
        }

        Vector2 aj = f.a;
        Vector2 bj = f.b;
        Vector2 dj = bj - aj;
        float lj = dj.magnitude;
        if (lj <= 1e-10f)
        {
            return false;
        }

        if (Mathf.Abs(li - lj) > 1e-5f)
        {
            return false;
        }

        Vector2 dni = di / li;
        Vector2 dnj = dj / lj;
        if ((dni + dnj).sqrMagnitude > dirEps)
        {
            return false;
        }

        Vector2 t1 = bj - ai;
        Vector2 t2 = aj - bi;
        if ((QuantizeVec2(t1, posQ) - QuantizeVec2(t2, posQ)).sqrMagnitude > transEps)
        {
            return false;
        }

        if (!TryGetQuantizedLatticeShift(t1, b1, b2, det, posQ, out Vector2 shift))
        {
            return false;
        }

        Vector2 qai = QuantizeVec2(ai + shift, posQ);
        Vector2 qbi = QuantizeVec2(bi + shift, posQ);
        Vector2 qaj = QuantizeVec2(aj, posQ);
        Vector2 qbj = QuantizeVec2(bj, posQ);

        return (qai - qbj).sqrMagnitude <= transEps && (qbi - qaj).sqrMagnitude <= transEps;
    }

    private static bool TryGetQuantizedLatticeShift(
        Vector2 t,
        Vector2 b1,
        Vector2 b2,
        float det,
        float posQ,
        out Vector2 shift)
    {
        shift = default;

        Vector2 uv = WorldToLattice(t, b1, b2, det);
        float mi = Mathf.Round(uv.x);
        float ni = Mathf.Round(uv.y);

        const float coeffEps = 1e-4f;
        if (Mathf.Abs(uv.x - mi) > coeffEps || Mathf.Abs(uv.y - ni) > coeffEps)
        {
            return false;
        }

        Vector2 s = mi * b1 + ni * b2;
        Vector2 qt = QuantizeVec2(t, posQ);
        Vector2 qs = QuantizeVec2(s, posQ);

        if ((qt - qs).sqrMagnitude > 1e-8f)
        {
            return false;
        }

        shift = qs;
        return true;
    }

    private static bool ValidateVertexAngleSumStrictUnionFind(
        List<CellRecord> records,
        List<BoundaryEdge> boundary,
        int[] pairMap,
        float posQ)
    {
        List<VertexInstance> instances = BuildVertexInstances(records, posQ);
        if (instances.Count == 0)
        {
            return false;
        }

        var pointToIds = new Dictionary<PointKey, List<int>>();
        for (int i = 0; i < instances.Count; i++)
        {
            PointKey key = instances[i].key;
            if (!pointToIds.TryGetValue(key, out List<int> ids))
            {
                ids = new List<int>();
                pointToIds.Add(key, ids);
            }

            ids.Add(i);
        }

        var dsu = new DisjointSet(instances.Count);

        foreach (var kv in pointToIds)
        {
            List<int> ids = kv.Value;
            for (int i = 1; i < ids.Count; i++)
            {
                dsu.Union(ids[0], ids[i]);
            }
        }

        for (int i = 0; i < boundary.Count; i++)
        {
            int j = pairMap[i];
            if (j < 0)
            {
                return false;
            }

            if (i > j)
            {
                continue;
            }

            PointKey a = new PointKey(boundary[i].a, posQ);
            PointKey b = new PointKey(boundary[i].b, posQ);
            PointKey c = new PointKey(boundary[j].a, posQ);
            PointKey d = new PointKey(boundary[j].b, posQ);

            if (!UnionPointClasses(dsu, pointToIds, a, d))
            {
                return false;
            }

            if (!UnionPointClasses(dsu, pointToIds, b, c))
            {
                return false;
            }
        }

        var sumByRoot = new Dictionary<int, float>();
        for (int i = 0; i < instances.Count; i++)
        {
            int root = dsu.Find(i);
            if (sumByRoot.TryGetValue(root, out float sum))
            {
                sumByRoot[root] = sum + instances[i].angle;
            }
            else
            {
                sumByRoot.Add(root, instances[i].angle);
            }
        }

        const float angleEps = 2e-2f;
        const float twoPi = 2f * Mathf.PI;

        foreach (var kv in sumByRoot)
        {
            if (Mathf.Abs(kv.Value - twoPi) > angleEps)
            {
                return false;
            }
        }

        return true;
    }

    private static List<VertexInstance> BuildVertexInstances(List<CellRecord> records, float posQ)
    {
        var list = new List<VertexInstance>();

        for (int i = 0; i < records.Count; i++)
        {
            Vector2[] verts = records[i].verts;
            int n = verts.Length;

            for (int v = 0; v < n; v++)
            {
                Vector2 prev = verts[(v - 1 + n) % n];
                Vector2 curr = verts[v];
                Vector2 next = verts[(v + 1) % n];

                Vector2 v1 = (prev - curr).normalized;
                Vector2 v2 = (next - curr).normalized;
                float angle = Vector2.Angle(v1, v2) * Mathf.Deg2Rad;

                list.Add(new VertexInstance
                {
                    key = new PointKey(curr, posQ),
                    angle = angle
                });
            }
        }

        return list;
    }

    private static bool UnionPointClasses(
        DisjointSet dsu,
        Dictionary<PointKey, List<int>> pointToIds,
        PointKey p0,
        PointKey p1)
    {
        if (!pointToIds.TryGetValue(p0, out List<int> a) || a.Count == 0)
        {
            return false;
        }

        if (!pointToIds.TryGetValue(p1, out List<int> b) || b.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            dsu.Union(a[i], b[0]);
        }

        for (int j = 1; j < b.Count; j++)
        {
            dsu.Union(b[0], b[j]);
        }

        return true;
    }

    private static List<DetectedCell> BuildMotif(
        List<CellRecord> records,
        Vector2 origin,
        Vector2 b1,
        Vector2 b2,
        float posQ)
    {
        _ = b1;
        _ = b2;

        var motif = new List<DetectedCell>(records.Count);

        for (int i = 0; i < records.Count; i++)
        {
            CellRecord r = records[i];
            Vector2 local = QuantizeVec2(r.pos - origin, posQ);

            motif.Add(new DetectedCell(
                r.shapeType,
                local,
                r.rotDeg,
                (int)r.shapeType));
        }

        return motif;
    }

    private static void ComputeShapeInfo(List<CellRecord> records, out CellShapeType baselineShape, out int shapeNum)
    {
        int minShape = int.MaxValue;
        var set = new HashSet<int>();

        for (int i = 0; i < records.Count; i++)
        {
            int v = (int)records[i].shapeType;
            set.Add(v);
            if (v < minShape)
            {
                minShape = v;
            }
        }

        baselineShape = (CellShapeType)minShape;
        shapeNum = set.Count;
    }

    private static void ReduceBasis(ref Vector2 b1, ref Vector2 b2)
    {
        for (int iter = 0; iter < 6; iter++)
        {
            if (b2.sqrMagnitude < b1.sqrMagnitude)
            {
                (b1, b2) = (b2, b1);
            }

            float denom = b1.sqrMagnitude;
            if (denom < 1e-12f)
            {
                return;
            }

            float mu = Mathf.Round(Vector2.Dot(b2, b1) / denom);
            b2 -= mu * b1;
        }

        if (Cross(b1, b2) < 0f)
        {
            b2 = -b2;
        }
    }

    private static Vector2 WorldToLattice(Vector2 d, Vector2 b1, Vector2 b2, float det)
    {
        float u = (d.x * b2.y - d.y * b2.x) / det;
        float v = (-d.x * b1.y + d.y * b1.x) / det;
        return new Vector2(u, v);
    }

    private static Vector2[] BuildWorldVertices(TileSO tile, Vector2 pos, float rotDeg, float cellScale)
    {
        Vector2[] src = tile.localVertices;
        int n = src.Length;
        Vector2[] dst = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            Vector2 p = src[i] * cellScale;
            dst[i] = PolygonSnapSolver.Rotate(p, rotDeg) + pos;
        }

        return dst;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private static float QuantizeFloat(float v, float scale)
    {
        if (scale <= 0f)
        {
            return v;
        }

        return Mathf.Round(v * scale) / scale;
    }

    private static Vector2 QuantizeVec2(Vector2 v, float scale)
    {
        if (scale <= 0f)
        {
            return v;
        }

        return new Vector2(
            Mathf.Round(v.x * scale) / scale,
            Mathf.Round(v.y * scale) / scale);
    }
}