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

    private readonly struct Signature
    {
        public readonly CellShapeType shapeType;
        public readonly int rotKey;

        public Signature(CellShapeType shapeType, int rotKey)
        {
            this.shapeType = shapeType;
            this.rotKey = rotKey;
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
    }

    private readonly struct GlobalKey
    {
        public readonly long x;
        public readonly long y;
        public readonly int shape;
        public readonly int rot;

        public GlobalKey(long x, long y, int shape, int rot)
        {
            this.x = x;
            this.y = y;
            this.shape = shape;
            this.rot = rot;
        }
    }

    private struct CellRecord
    {
        public CellShapeType shapeType;
        public Vector2 pos;
        public float rotDeg;
        public int rotKey;
    }

    private struct CandidateVector
    {
        public Vector2 v;
        public float score;
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
        result = default;
        reason = string.Empty;

        if (placedTiles == null || placedTiles.Count < 1)
        {
            reason = "场上没有样本。";
            return false;
        }

        if (cellScale <= 0f)
        {
            reason = "cellScale 必须 > 0。";
            return false;
        }

        var records = BuildRecords(placedTiles, mainDataSO, positionQuantizeScale, rotationQuantizeScale);
        if (records.Count < 1)
        {
            reason = "有效单元不足。";
            return false;
        }

        Rect bounds = BuildBounds(records);
        Rect inner = ShrinkRect(bounds, Mathf.Max(cellScale, minTranslationLength) * 1.5f);
        var index = BuildGlobalIndex(records, positionQuantizeScale);

        Vector2 origin;
        Vector2 b1;
        Vector2 b2;
        bool usedFallback = false;

        List<CandidateVector> strictCandidates = BuildCandidateVectors(
            records,
            index,
            inner,
            bounds,
            positionQuantizeScale,
            minTranslationLength,
            symmetryScoreThreshold);

        if (strictCandidates.Count >= 2 && TrySelectBasis(strictCandidates, cellScale, out b1, out b2))
        {
            origin = records[0].pos;
        }
        else
        {
            // 放宽约束：不要求高分，只要能给出一个可行周期基
            List<CandidateVector> looseCandidates = BuildCandidateVectors(
                records,
                index,
                inner,
                bounds,
                positionQuantizeScale,
                Mathf.Max(1e-4f, minTranslationLength * 0.25f),
                0f);

            if (looseCandidates.Count >= 2 && TrySelectBasis(looseCandidates, cellScale, out b1, out b2))
            {
                origin = records[0].pos;
                usedFallback = true;
            }
            else if (!TryBuildAnyBasis(records, placedTiles, mainDataSO, cellScale, out origin, out b1, out b2))
            {
                reason = "未能构造可行基向量。";
                return false;
            }
            else
            {
                usedFallback = true;
            }
        }

        ReduceBasis(ref b1, ref b2);

        List<DetectedCell> motif = BuildMotif(records, b1, b2, origin, positionQuantizeScale);
        if (motif.Count == 0)
        {
            reason = "提取到的 motif 为空。";
            return false;
        }

        float passThreshold = usedFallback ? 0.5f : symmetryScoreThreshold;
        if (!ValidateRetiling(records, index, motif, origin, b1, b2, inner, positionQuantizeScale, passThreshold))
        {
            reason = "回铺验证未通过。";
            return false;
        }

        ComputeShapeInfo(records, out CellShapeType baselineShape, out int shapeNum);
        result = new DetectionResult(origin, b1, b2, motif, baselineShape, shapeNum);
        return true;
    }

    private static bool TryBuildAnyBasis(
    List<CellRecord> records,
    IReadOnlyList<PlacedTileData> placedTiles,
    MainDataSO mainDataSO,
    float cellScale,
    out Vector2 origin,
    out Vector2 b1,
    out Vector2 b2)
    {
        origin = records[0].pos;
        b1 = default;
        b2 = default;

        // 有至少两个中心点：用中心差构造一个平移，再取正交向量
        if (records.Count >= 2)
        {
            float bestLen2 = 0f;
            Vector2 best = default;

            for (int i = 0; i < records.Count; i++)
            {
                for (int j = i + 1; j < records.Count; j++)
                {
                    Vector2 d = records[j].pos - records[i].pos;
                    float l2 = d.sqrMagnitude;
                    if (l2 > bestLen2)
                    {
                        bestLen2 = l2;
                        best = d;
                    }
                }
            }

            if (bestLen2 > 1e-8f)
            {
                b1 = best;
                b2 = new Vector2(-best.y, best.x);
                return b2.sqrMagnitude > 1e-8f;
            }
        }

        // 仅 1 个样本：按首个图形边长构造一个方形周期基（“任意可行解”兜底）
        PlacedTileData p = placedTiles[0];
        if (p.tileIndex < 0 || p.tileIndex >= mainDataSO.tiles.Count || mainDataSO.tiles[p.tileIndex] == null)
        {
            return false;
        }

        TileSO tile = mainDataSO.tiles[p.tileIndex];
        if (tile.localVertices == null || tile.localVertices.Length < 2)
        {
            return false;
        }

        float edgeLen = (tile.localVertices[1] - tile.localVertices[0]).magnitude * cellScale;
        if (edgeLen <= 1e-6f)
        {
            return false;
        }

        float rot = p.rotationDeg;
        b1 = PolygonSnapSolver.Rotate(new Vector2(edgeLen, 0f), rot);
        b2 = PolygonSnapSolver.Rotate(new Vector2(0f, edgeLen), rot);
        return true;
    }

    private static List<CellRecord> BuildRecords(
        IReadOnlyList<PlacedTileData> placedTiles,
        MainDataSO mainDataSO,
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
            if (tile == null)
            {
                continue;
            }

            Vector2 qPos = QuantizeVec2(p.position, posQ);
            float qRot = QuantizeFloat(p.rotationDeg, rotQ);
            int rotKey = Mathf.RoundToInt(qRot * rotQ);

            list.Add(new CellRecord
            {
                shapeType = tile.shapeType,
                pos = qPos,
                rotDeg = qRot,
                rotKey = rotKey
            });
        }

        return list;
    }

    private static Dictionary<GlobalKey, byte> BuildGlobalIndex(List<CellRecord> records, float posQ)
    {
        var map = new Dictionary<GlobalKey, byte>(records.Count * 2);
        for (int i = 0; i < records.Count; i++)
        {
            CellRecord r = records[i];
            long qx = (long)Mathf.Round(r.pos.x * posQ);
            long qy = (long)Mathf.Round(r.pos.y * posQ);
            var key = new GlobalKey(qx, qy, (int)r.shapeType, r.rotKey);
            if (!map.ContainsKey(key))
            {
                map.Add(key, 0);
            }
        }

        return map;
    }

    private static List<CandidateVector> BuildCandidateVectors(
        List<CellRecord> records,
        Dictionary<GlobalKey, byte> index,
        Rect inner,
        Rect bounds,
        float posQ,
        float minLen,
        float threshold)
    {
        var bySig = new Dictionary<Signature, List<int>>();
        for (int i = 0; i < records.Count; i++)
        {
            var sig = new Signature(records[i].shapeType, records[i].rotKey);
            if (!bySig.TryGetValue(sig, out List<int> group))
            {
                group = new List<int>();
                bySig.Add(sig, group);
            }

            group.Add(i);
        }

        var dedup = new HashSet<VectorKey>();
        var candidates = new List<CandidateVector>();

        foreach (var kv in bySig)
        {
            List<int> g = kv.Value;
            for (int i = 0; i < g.Count; i++)
            {
                Vector2 p0 = records[g[i]].pos;
                for (int j = i + 1; j < g.Count; j++)
                {
                    Vector2 p1 = records[g[j]].pos;
                    Vector2 v = p1 - p0;
                    if (v.magnitude < minLen)
                    {
                        continue;
                    }

                    VectorKey vk = new VectorKey(v, posQ);
                    if (!dedup.Add(vk))
                    {
                        continue;
                    }

                    float score = ScoreTranslation(records, index, inner, bounds, v, posQ);
                    if (score >= threshold)
                    {
                        candidates.Add(new CandidateVector
                        {
                            v = v,
                            score = score
                        });
                    }

                    Vector2 vn = -v;
                    VectorKey vkn = new VectorKey(vn, posQ);
                    if (dedup.Add(vkn))
                    {
                        float scoreN = ScoreTranslation(records, index, inner, bounds, vn, posQ);
                        if (scoreN >= threshold)
                        {
                            candidates.Add(new CandidateVector
                            {
                                v = vn,
                                score = scoreN
                            });
                        }
                    }
                }
            }
        }

        return candidates;
    }

    private static float ScoreTranslation(
        List<CellRecord> records,
        Dictionary<GlobalKey, byte> index,
        Rect inner,
        Rect bounds,
        Vector2 v,
        float posQ)
    {
        int total = 0;
        int hit = 0;

        for (int i = 0; i < records.Count; i++)
        {
            CellRecord r = records[i];
            if (!inner.Contains(r.pos))
            {
                continue;
            }

            Vector2 targetPos = r.pos + v;
            if (!bounds.Contains(targetPos))
            {
                continue;
            }

            total++;

            Vector2 qt = QuantizeVec2(targetPos, posQ);
            long qx = (long)Mathf.Round(qt.x * posQ);
            long qy = (long)Mathf.Round(qt.y * posQ);
            var key = new GlobalKey(qx, qy, (int)r.shapeType, r.rotKey);

            if (index.ContainsKey(key))
            {
                hit++;
            }
        }

        if (total <= 0)
        {
            return 0f;
        }

        return (float)hit / total;
    }

    private static bool TrySelectBasis(List<CandidateVector> candidates, float cellScale, out Vector2 b1, out Vector2 b2)
    {
        b1 = default;
        b2 = default;

        float bestCost = float.PositiveInfinity;
        float areaEps = Mathf.Max(1e-6f, 0.01f * cellScale * cellScale);

        for (int i = 0; i < candidates.Count; i++)
        {
            for (int j = i + 1; j < candidates.Count; j++)
            {
                Vector2 v1 = candidates[i].v;
                Vector2 v2 = candidates[j].v;
                float area = Mathf.Abs(Cross(v1, v2));
                if (area <= areaEps)
                {
                    continue;
                }

                float pairScore = 0.5f * (candidates[i].score + candidates[j].score);
                float cost = area / Mathf.Max(pairScore, 1e-4f);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    b1 = v1;
                    b2 = v2;
                }
            }
        }

        return !float.IsPositiveInfinity(bestCost);
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

    private static List<DetectedCell> BuildMotif(
        List<CellRecord> records,
        Vector2 b1,
        Vector2 b2,
        Vector2 origin,
        float posQ)
    {
        float det = b1.x * b2.y - b1.y * b2.x;
        if (Mathf.Abs(det) < 1e-10f)
        {
            return new List<DetectedCell>();
        }

        var dedup = new HashSet<string>();
        var motif = new List<DetectedCell>();

        for (int i = 0; i < records.Count; i++)
        {
            CellRecord r = records[i];
            Vector2 d = r.pos - origin;

            float u = (d.x * b2.y - d.y * b2.x) / det;
            float v = (-d.x * b1.y + d.y * b1.x) / det;

            u = Wrap01(u);
            v = Wrap01(v);

            Vector2 local = QuantizeVec2(u * b1 + v * b2, posQ);

            string key = $"{(int)r.shapeType}|{r.rotKey}|{Mathf.RoundToInt(local.x * posQ)}|{Mathf.RoundToInt(local.y * posQ)}";
            if (!dedup.Add(key))
            {
                continue;
            }

            motif.Add(new DetectedCell(
                r.shapeType,
                local,
                r.rotDeg,
                (int)r.shapeType));
        }

        return motif;
    }

    private static bool ValidateRetiling(
        List<CellRecord> records,
        Dictionary<GlobalKey, byte> index,
        List<DetectedCell> motif,
        Vector2 origin,
        Vector2 b1,
        Vector2 b2,
        Rect inner,
        float posQ,
        float passThreshold)
    {
        if (motif.Count == 0)
        {
            return false;
        }

        float det = b1.x * b2.y - b1.y * b2.x;
        if (Mathf.Abs(det) < 1e-10f)
        {
            return false;
        }

        int actualTotal = 0;
        int actualMatched = 0;

        for (int i = 0; i < records.Count; i++)
        {
            CellRecord r = records[i];
            if (!inner.Contains(r.pos))
            {
                continue;
            }

            actualTotal++;
            Vector2 d = r.pos - origin;
            float u = (d.x * b2.y - d.y * b2.x) / det;
            float v = (-d.x * b1.y + d.y * b1.x) / det;

            Vector2 local = QuantizeVec2(Wrap01(u) * b1 + Wrap01(v) * b2, posQ);
            bool found = false;
            for (int m = 0; m < motif.Count; m++)
            {
                DetectedCell mc = motif[m];
                if (mc.shapeType != r.shapeType)
                {
                    continue;
                }

                int mk = Mathf.RoundToInt(mc.localRotationDeg * 10000f);
                if (mk != r.rotKey)
                {
                    continue;
                }

                if ((mc.localCenter - local).sqrMagnitude <= 1e-8f)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                actualMatched++;
            }
        }

        if (actualTotal == 0)
        {
            return false;
        }

        float recall = (float)actualMatched / actualTotal;
        return recall >= passThreshold;
    }

    private static Rect BuildBounds(List<CellRecord> records)
    {
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < records.Count; i++)
        {
            Vector2 p = records[i].pos;
            if (p.x < minX)
                minX = p.x;
            if (p.y < minY)
                minY = p.y;
            if (p.x > maxX)
                maxX = p.x;
            if (p.y > maxY)
                maxY = p.y;
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static Rect ShrinkRect(Rect rect, float margin)
    {
        float minX = rect.xMin + margin;
        float maxX = rect.xMax - margin;
        float minY = rect.yMin + margin;
        float maxY = rect.yMax - margin;

        if (minX >= maxX || minY >= maxY)
        {
            return rect;
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
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

    private static float Wrap01(float x)
    {
        x -= Mathf.Floor(x);
        if (x > 0.99999f || x < 0.00001f)
        {
            return 0f;
        }

        return x;
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