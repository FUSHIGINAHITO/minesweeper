using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

public partial class PolygonPlacer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private MainDataSO mainDataSO;
    [SerializeField] private PolygonPlacementView view;
    [SerializeField] private Transform placedRoot;
    [SerializeField] private Transform previewRoot;

    [Header("Auto Buttons")]
    [SerializeField] private PolygonButton tileButtonPrefab;
    [SerializeField] private Transform tileButtonRoot;

    [Header("Placement Plane (2D XY)")]
    [SerializeField] private float placementZ = 0f;

    [Header("Cell")]
    [SerializeField] private float cellScale = 1f;

    [Header("Pose Quantization")]
    [SerializeField] private float positionQuantizeScale = 100000f; // 1e-5
    [SerializeField] private float rotationQuantizeScale = 10000f;  // 1e-4 deg

    [Header("Snap")]
    [SerializeField] private float edgeSnapTolerance = 0.02f;
    [SerializeField] private float edgeQuantizeScale = 10000f;
    [SerializeField, Range(0f, 180f)] private float maxSnapRotateDeg = 90f;
    [SerializeField, Range(0f, 30f)] private float nearestNormalTieDeg = 3f;
    [SerializeField] private float snapNormalGapTolerance = 0.04f;
    [SerializeField] private float snapTangentialGapTolerance = 0.04f;

    [Header("Overlap")]
    [SerializeField] private float overlapAreaBlockThreshold = 0.0001f;
    [SerializeField] private bool logOverlapArea = false;
    [SerializeField] private float debugOverlapArea;

    [Header("Preview")]
    [SerializeField, Range(0.05f, 1f)] private float previewAlpha = 0.45f;
    [SerializeField] private Color previewValidColor = Color.white;
    [SerializeField] private Color previewInvalidColor = Color.red;

    [Header("Render Order")]
    [SerializeField] private int placedOrderStart = 0;

    [Header("Periodic Motif Export")]
    [SerializeField] private string exportMotifId = "NewMotif";
    [SerializeField] private string exportAssetFolder = "Assets/Generated/PeriodicMotifs";
    [SerializeField, Min(0.0001f)] private float detectMinTranslationLength = 0.2f;
    [SerializeField, Range(0.5f, 1f)] private float detectScoreThreshold = 0.98f;

    private readonly PolygonBoundaryTracker boundaryTracker = new();
    private readonly PolygonSnapSolver snapSolver = new();
    private readonly PeriodicMotifDetector motifDetector = new();

    private int currentTileIndex = -1;
    private float heldRotationDeg;
    private float snappedRotationDeg;
    private Vector3 snappedPos;
    private bool hasSnapSolution;
    private bool previewCanPlace;

    private int selectedSnapEdgeIndex = -1;
    private int detectedSnapEdgeIndex = -1;
    private bool hasActiveBoundaryEdge;
    private BoundaryEdge activeBoundaryEdge;
    private bool isSnapLatched;

    private Vector2 currentMouseWorld2D;
    private int nextPlacedOrder;

    // overlap gizmo data
    private bool gizmoHasOverlap;
    private Vector2[] gizmoOverlapHandPolygon;
    private Vector2[] gizmoOverlapPlacedPolygon;

    // Enter 成功回铺后锁定编辑，仅 Backspace 可重置
    private bool placementLocked;
    private PeriodicMotifSO cachedMotifForExport;

    private void Start()
    {
        nextPlacedOrder = placedOrderStart;
        BuildTileButtonsOnce();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            ClearPlacedTiles();
        }

        // Enter: 判定 + 直接回铺（不导出）
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!placementLocked)
            {
                TryDetectAndRebuildTiling();
            }
        }

        // P: 仅导出 SO
        if (Input.GetKeyDown(KeyCode.P))
        {
            TryDetectAndExportOnly();
        }

        if (placementLocked)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelHolding();
        }

        if (currentTileIndex < 0 || !view.HasPreview)
        {
            return;
        }

        if (TryGetMouseWorldOnPlacementPlane(out Vector3 mouseWorld))
        {
            currentMouseWorld2D = new Vector2(mouseWorld.x, mouseWorld.y);
            UpdatePlacementState(mouseWorld);

            if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.R))
                && boundaryTracker.PlacedTileCount > 0
                && hasActiveBoundaryEdge
                && hasSnapSolution
                && isSnapLatched)
            {
                RotateToNextSnapEdge();
            }
        }
        else
        {
            hasSnapSolution = false;
            previewCanPlace = false;
            hasActiveBoundaryEdge = false;
            isSnapLatched = false;
            selectedSnapEdgeIndex = -1;
            detectedSnapEdgeIndex = -1;
            ClearOverlapGizmoState();
        }

        view.SetPreviewPose(snappedPos, snappedRotationDeg, cellScale, nextPlacedOrder);
        view.SetPreviewVisual(previewCanPlace, previewAlpha, previewValidColor, previewInvalidColor);

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            PlaceCurrent();
        }
    }

    private void BuildTileButtonsOnce()
    {
        var colors = ColorGenerator.GeneratePerceptualHueCycleColors(mainDataSO, mainDataSO.tiles.Count);

        for (int i = 0; i < mainDataSO.tiles.Count; i++)
        {
            TileSO tile = mainDataSO.tiles[i];
            PolygonButton btn = Instantiate(tileButtonPrefab, tileButtonRoot);
            int capturedIndex = i;

            btn.image.sprite = tile.polygonSprite;
            var color = colors[i];
            color.a = 0.8f;
            btn.image.color = color;
            btn.button.onClick.AddListener(() => SelectTileByIndex(capturedIndex));
            btn.name = $"TileButton_{capturedIndex}_{tile.shapeType}";
        }
    }

    public void SelectTileByIndex(int index)
    {
        if (placementLocked)
        {
            return;
        }

        currentTileIndex = index;
        heldRotationDeg = 0f;
        snappedRotationDeg = 0f;
        selectedSnapEdgeIndex = -1;
        detectedSnapEdgeIndex = -1;
        isSnapLatched = false;

        hasSnapSolution = false;
        previewCanPlace = false;
        hasActiveBoundaryEdge = false;
        ClearOverlapGizmoState();

        Vector3 pos = new(0f, 0f, placementZ);
        if (TryGetMouseWorldOnPlacementPlane(out Vector3 worldPos))
        {
            pos = worldPos;
        }

        snappedPos = pos;
        snappedRotationDeg = heldRotationDeg;

        view.RebuildPreview(mainDataSO.tiles[currentTileIndex], pos, snappedRotationDeg, cellScale, previewRoot, nextPlacedOrder);
        view.SetPreviewVisual(boundaryTracker.PlacedTileCount == 0, previewAlpha, previewValidColor, previewInvalidColor);
    }

    public void CancelHolding()
    {
        currentTileIndex = -1;
        selectedSnapEdgeIndex = -1;
        detectedSnapEdgeIndex = -1;
        hasSnapSolution = false;
        previewCanPlace = false;
        hasActiveBoundaryEdge = false;
        isSnapLatched = false;
        ClearOverlapGizmoState();
        view.ClearPreview();
    }

    private void UpdatePlacementState(Vector3 mouseWorld)
    {
        TileSO tile = mainDataSO.tiles[currentTileIndex];
        view.EnsurePreviewShape(tile, mouseWorld, heldRotationDeg, cellScale, previewRoot, nextPlacedOrder);

        if (boundaryTracker.PlacedTileCount == 0)
        {
            snappedPos = mouseWorld;
            snappedRotationDeg = heldRotationDeg;
            hasSnapSolution = true;
            previewCanPlace = true;
            hasActiveBoundaryEdge = false;
            selectedSnapEdgeIndex = -1;
            detectedSnapEdgeIndex = -1;
            isSnapLatched = false;
            ClearOverlapGizmoState();
            return;
        }

        boundaryTracker.EnsureBoundaryEdges(mainDataSO, cellScale, edgeQuantizeScale);

        Vector2 center = new(mouseWorld.x, mouseWorld.y);

        if (!snapSolver.TryFindNearestSnapPair(
                tile,
                center,
                heldRotationDeg,
                boundaryTracker.BoundaryEdges,
                cellScale,
                maxSnapRotateDeg,
                nearestNormalTieDeg,
                snapNormalGapTolerance,
                snapTangentialGapTolerance,
                out BoundaryEdge edge,
                out int detectedEdgeIndex))
        {
            hasSnapSolution = false;
            previewCanPlace = false;
            hasActiveBoundaryEdge = false;
            selectedSnapEdgeIndex = -1;
            detectedSnapEdgeIndex = -1;
            isSnapLatched = false;
            snappedPos = mouseWorld;
            snappedRotationDeg = heldRotationDeg;
            ClearOverlapGizmoState();
            return;
        }

        if (!isSnapLatched)
        {
            activeBoundaryEdge = edge;
            selectedSnapEdgeIndex = detectedEdgeIndex;
            detectedSnapEdgeIndex = detectedEdgeIndex;
            isSnapLatched = true;
        }

        hasActiveBoundaryEdge = true;

        if (snapSolver.TrySolveSnapPoseForSpecificEdge(
                tile,
                activeBoundaryEdge,
                selectedSnapEdgeIndex,
                cellScale,
                edgeSnapTolerance,
                out Vector2 pos,
                out float rotDeg))
        {
            snappedPos = new Vector3(pos.x, pos.y, placementZ);
            snappedRotationDeg = rotDeg;
            hasSnapSolution = true;

            UpdateOverlapAndCanPlace(new Vector2(pos.x, pos.y), rotDeg);
        }
        else
        {
            hasSnapSolution = false;
            previewCanPlace = false;
            hasActiveBoundaryEdge = false;
            selectedSnapEdgeIndex = -1;
            detectedSnapEdgeIndex = -1;
            isSnapLatched = false;
            snappedPos = mouseWorld;
            snappedRotationDeg = heldRotationDeg;
            ClearOverlapGizmoState();
        }
    }

    private void RotateToNextSnapEdge()
    {
        TileSO tile = mainDataSO.tiles[currentTileIndex];
        int edgeCount = tile.localVertices.Length;
        if (edgeCount < 3 || !hasActiveBoundaryEdge)
        {
            return;
        }

        int start = selectedSnapEdgeIndex;
        if (start < 0 || start >= edgeCount)
        {
            start = 0;
        }

        int idx = (start + 1) % edgeCount;
        selectedSnapEdgeIndex = idx;

        if (snapSolver.TrySolveSnapPoseForSpecificEdge(
                tile,
                activeBoundaryEdge,
                idx,
                cellScale,
                edgeSnapTolerance,
                out Vector2 pos,
                out float rotDeg))
        {
            snappedRotationDeg = rotDeg;
            snappedPos = new Vector3(pos.x, pos.y, placementZ);

            hasSnapSolution = true;
            UpdateOverlapAndCanPlace(new Vector2(pos.x, pos.y), rotDeg);
        }
        else
        {
            hasSnapSolution = false;
            previewCanPlace = false;
            hasActiveBoundaryEdge = false;
            isSnapLatched = false;
            selectedSnapEdgeIndex = -1;
            detectedSnapEdgeIndex = -1;
            ClearOverlapGizmoState();
        }
    }

    private void PlaceCurrent()
    {
        if (placementLocked || !hasSnapSolution || !previewCanPlace || currentTileIndex < 0)
        {
            return;
        }

        TileSO tile = mainDataSO.tiles[currentTileIndex];

        Vector2 qPos2 = QuantizeVec2(new Vector2(snappedPos.x, snappedPos.y), positionQuantizeScale);
        float qRot = QuantizeFloat(snappedRotationDeg, rotationQuantizeScale);

        Vector3 exactPos = new(qPos2.x, qPos2.y, placementZ);
        float exactRot = qRot;

        view.AddPlaced(tile, exactPos, exactRot, cellScale, placedRoot, currentTileIndex);
        nextPlacedOrder++;

        boundaryTracker.AddPlacedTile(currentTileIndex, qPos2, exactRot);

        isSnapLatched = false;
        selectedSnapEdgeIndex = -1;
        detectedSnapEdgeIndex = -1;
        hasActiveBoundaryEdge = false;
        ClearOverlapGizmoState();
    }

    private void UpdateOverlapAndCanPlace(Vector2 pos, float rotDeg)
    {
        Vector2 qPos = QuantizeVec2(pos, positionQuantizeScale);
        float qRot = QuantizeFloat(rotDeg, rotationQuantizeScale);

        if (boundaryTracker.TryGetOverlapPair(
                currentTileIndex,
                qPos,
                qRot,
                mainDataSO,
                cellScale,
                out Vector2[] handPoly,
                out Vector2[] placedPoly,
                out float overlapArea))
        {
            gizmoHasOverlap = true;
            gizmoOverlapHandPolygon = handPoly;
            gizmoOverlapPlacedPolygon = placedPoly;
            debugOverlapArea = overlapArea;

            previewCanPlace = overlapArea <= overlapAreaBlockThreshold;

            if (logOverlapArea && !previewCanPlace)
            {
                Debug.Log($"[PolygonPlacer] overlapArea={overlapArea:F6}, threshold={overlapAreaBlockThreshold:F6}");
            }

            return;
        }

        debugOverlapArea = 0f;
        ClearOverlapGizmoState();
        previewCanPlace = true;
    }

    private bool TryDetectCurrent(out PeriodicMotifDetector.DetectionResult detectResult)
    {
        detectResult = default;

        if (boundaryTracker.PlacedTileCount < 1)
        {
            Debug.LogWarning("[PolygonPlacer] 场上没有单元，无法判定密铺。");
            return false;
        }

        List<PlacedTileData> snapshot = boundaryTracker.GetPlacedTilesSnapshot();

        if (!motifDetector.TryDetect(
                snapshot,
                mainDataSO,
                cellScale,
                positionQuantizeScale,
                rotationQuantizeScale,
                detectMinTranslationLength,
                detectScoreThreshold,
                out detectResult,
                out string reason))
        {
            string code = "UNKNOWN";
            string message = reason;

            int sep = reason.IndexOf('|');
            if (sep > 0 && sep < reason.Length - 1)
            {
                code = reason.Substring(0, sep);
                message = reason.Substring(sep + 1);
            }

            Debug.LogWarning($"[PolygonPlacer][{code}] 密铺判定失败：{message}");
            return false;
        }

        return true;
    }

    // Enter: 判定 + 回铺，不导出
    private void TryDetectAndRebuildTiling()
    {
        if (!TryDetectCurrent(out PeriodicMotifDetector.DetectionResult detectResult))
        {
            return;
        }

        List<PlacedTileData> sourceSnapshot = boundaryTracker.GetPlacedTilesSnapshot();
        PeriodicMotifSO motifSo = BuildMotifSO(detectResult, sourceSnapshot);

        if (!TryRebuildScreenTilingFromMotif(motifSo, sourceSnapshot, out string fillError))
        {
            Destroy(motifSo);
            Debug.LogError($"[PolygonPlacer] 回铺失败：{fillError}");
            return;
        }

        CacheMotifForExport(motifSo);
        Destroy(motifSo);

        placementLocked = true;
        CancelHolding();
        Debug.Log("[PolygonPlacer] 判定成功，已完成屏幕范围回铺，编辑已锁定。按 Backspace 可重置。");
    }

    // P: 判定 + 回铺 + 导出；若已锁定则直接导出缓存
    private void TryDetectAndExportOnly()
    {
        if (placementLocked)
        {
            TryExportCachedMotif();
            return;
        }

        if (!TryDetectCurrent(out PeriodicMotifDetector.DetectionResult detectResult))
        {
            return;
        }

        List<PlacedTileData> sourceSnapshot = boundaryTracker.GetPlacedTilesSnapshot();
        PeriodicMotifSO motifSo = BuildMotifSO(detectResult, sourceSnapshot);

        if (!TryRebuildScreenTilingFromMotif(motifSo, sourceSnapshot, out string fillError))
        {
            Destroy(motifSo);
            Debug.LogError($"[PolygonPlacer] 回铺失败：{fillError}");
            return;
        }

        CacheMotifForExport(motifSo);
        Destroy(motifSo);

        placementLocked = true;
        CancelHolding();

        Debug.Log("[PolygonPlacer] 判定成功，已完成屏幕范围回铺，开始导出基元。");
        TryExportCachedMotif();
    }

    private PeriodicMotifSO BuildMotifSO(
    PeriodicMotifDetector.DetectionResult detectResult,
    List<PlacedTileData> sourcePlacedTiles)
    {
        string motifId = string.IsNullOrWhiteSpace(exportMotifId)
            ? $"Motif_{System.DateTime.Now:yyyyMMdd_HHmmss}"
            : exportMotifId.Trim();

        var so = ScriptableObject.CreateInstance<PeriodicMotifSO>();
        so.baselineShape = detectResult.baselineShape;
        so.shapeNum = Mathf.Max(1, detectResult.shapeNum);
        so.positionQuantizeScale = Mathf.Max(1f, positionQuantizeScale);
        so.rotationQuantizeScale = Mathf.Max(1f, rotationQuantizeScale);

        Vector2 b1Unit = detectResult.basis1 / cellScale;
        Vector2 b2Unit = detectResult.basis2 / cellScale;
        so.basis1Unit = so.QuantizePosition(b1Unit);
        so.basis2Unit = so.QuantizePosition(b2Unit);

        var cells = new List<MotifCellData>(sourcePlacedTiles.Count);

        for (int i = 0; i < sourcePlacedTiles.Count; i++)
        {
            PlacedTileData p = sourcePlacedTiles[i];
            if (p.tileIndex < 0 || p.tileIndex >= mainDataSO.tiles.Count)
            {
                continue;
            }

            TileSO tile = mainDataSO.tiles[p.tileIndex];
            if (tile == null)
            {
                continue;
            }

            Vector2 qPos = QuantizeVec2(p.position, so.positionQuantizeScale);
            float qRot = QuantizeFloat(p.rotationDeg, so.rotationQuantizeScale);

            // 关键：按“场上样本”直接构造 local，不做 Wrap01/去重
            Vector2 localCenter = qPos - detectResult.origin;

            cells.Add(new MotifCellData
            {
                shapeType = tile.shapeType,
                localCenterUnit = so.QuantizePosition(localCenter / cellScale),
                localRotationDeg = so.QuantizeRotation(qRot),
            });
        }

        if (cells.Count == 0)
        {
            // 兜底（理论上不会走到）
            cells.Add(new MotifCellData
            {
                shapeType = detectResult.baselineShape,
                localCenterUnit = Vector2.zero,
                localRotationDeg = 0f,
            });
        }

        so.cells = cells.ToArray();
        so.shapeNum = Mathf.Max(1, so.shapeNum);
        return so;
    }

    private void CacheMotifForExport(PeriodicMotifSO source)
    {
        if (source == null)
        {
            return;
        }

        if (cachedMotifForExport != null)
        {
            Destroy(cachedMotifForExport);
            cachedMotifForExport = null;
        }

        cachedMotifForExport = CloneMotifSO(source);
    }

    private static PeriodicMotifSO CloneMotifSO(PeriodicMotifSO source)
    {
        var clone = ScriptableObject.CreateInstance<PeriodicMotifSO>();
        clone.baselineShape = source.baselineShape;
        clone.shapeNum = source.shapeNum;
        clone.basis1Unit = source.basis1Unit;
        clone.basis2Unit = source.basis2Unit;
        clone.positionQuantizeScale = source.positionQuantizeScale;
        clone.rotationQuantizeScale = source.rotationQuantizeScale;

        int len = source.cells == null ? 0 : source.cells.Length;
        clone.cells = new MotifCellData[len];
        for (int i = 0; i < len; i++)
        {
            clone.cells[i] = source.cells[i];
        }

        return clone;
    }

    private void TryExportCachedMotif()
    {
#if UNITY_EDITOR
        if (cachedMotifForExport == null)
        {
            Debug.LogWarning("[PolygonPlacer] 没有可导出的缓存基元。请先 Enter 回铺或按 P 执行判定。");
            return;
        }

        PeriodicMotifSO exportSo = CloneMotifSO(cachedMotifForExport);

        if (!TryCreatePeriodicMotifAsset(exportSo, out string assetPath, out string error))
        {
            Destroy(exportSo);
            Debug.LogError($"[PolygonPlacer] 导出失败：{error}");
            return;
        }

        Debug.Log($"[PolygonPlacer] 导出成功：{assetPath}");
#else
    Debug.LogWarning("[PolygonPlacer] 当前不在 UnityEditor，无法导出 Asset。");
#endif
    }

    private bool TryRebuildScreenTilingFromMotif(
    PeriodicMotifSO motifSo,
    List<PlacedTileData> anchorSnapshot,
    out string reason)
    {
        reason = string.Empty;

        if (motifSo == null || motifSo.cells == null || motifSo.cells.Length == 0)
        {
            reason = "motif 数据为空。";
            return false;
        }

        if (!TryGetScreenRectOnPlacementPlane(out Rect screenRect))
        {
            reason = "无法计算屏幕范围。";
            return false;
        }

        Vector2 b1 = motifSo.basis1Unit * cellScale;
        Vector2 b2 = motifSo.basis2Unit * cellScale;
        float det = b1.x * b2.y - b1.y * b2.x;
        if (Mathf.Abs(det) < 1e-10f)
        {
            reason = "基向量退化（共线）。";
            return false;
        }

        var shapeToTile = new Dictionary<CellShapeType, (int index, TileSO tile)>();
        for (int i = 0; i < mainDataSO.tiles.Count; i++)
        {
            TileSO t = mainDataSO.tiles[i];
            if (t != null && !shapeToTile.ContainsKey(t.shapeType))
            {
                shapeToTile.Add(t.shapeType, (i, t));
            }
        }

        for (int i = 0; i < motifSo.cells.Length; i++)
        {
            if (!shapeToTile.ContainsKey(motifSo.cells[i].shapeType))
            {
                reason = $"缺少 shapeType={motifSo.cells[i].shapeType} 对应 TileSO。";
                return false;
            }
        }

        view.ClearPlaced();
        boundaryTracker.Clear();
        nextPlacedOrder = placedOrderStart;
        ClearOverlapGizmoState();

        // 关键改动：优先用“回铺前样本 + motif 同索引 cell”反推 origin，保持原有基元不漂移
        Vector2 origin = ResolveRebuildOrigin(motifSo, anchorSnapshot, screenRect);

        Vector2[] corners =
        {
            new Vector2(screenRect.xMin, screenRect.yMin),
            new Vector2(screenRect.xMin, screenRect.yMax),
            new Vector2(screenRect.xMax, screenRect.yMin),
            new Vector2(screenRect.xMax, screenRect.yMax)
        };

        float minI = float.PositiveInfinity;
        float maxI = float.NegativeInfinity;
        float minJ = float.PositiveInfinity;
        float maxJ = float.NegativeInfinity;

        for (int k = 0; k < corners.Length; k++)
        {
            Vector2 d = corners[k] - origin;
            float i = (d.x * b2.y - d.y * b2.x) / det;
            float j = (-d.x * b1.y + d.y * b1.x) / det;

            if (i < minI)
                minI = i;
            if (i > maxI)
                maxI = i;
            if (j < minJ)
                minJ = j;
            if (j > maxJ)
                maxJ = j;
        }

        const int latticePadding = 3;
        int iMin = Mathf.FloorToInt(minI) - latticePadding;
        int iMax = Mathf.CeilToInt(maxI) + latticePadding;
        int jMin = Mathf.FloorToInt(minJ) - latticePadding;
        int jMax = Mathf.CeilToInt(maxJ) + latticePadding;

        int latticeWidth = Mathf.Max(1, iMax - iMin + 1);
        int latticeHeight = Mathf.Max(1, jMax - jMin + 1);
        int latticeCount = Mathf.Max(1, latticeWidth * latticeHeight);
        Color[] latticeColors = ColorGenerator.GeneratePerceptualHueCycleColors(mainDataSO, latticeCount);
        int latticeColorIndex = 0;

        var placedKeys = new HashSet<(long x, long y, CellShapeType shape, int rotKey)>();

        for (int j = jMin; j <= jMax; j++)
        {
            for (int i = iMin; i <= iMax; i++)
            {
                Color copyColor = latticeColors[latticeColorIndex % latticeColors.Length];
                latticeColorIndex++;

                Vector2 latticeBase = origin + i * b1 + j * b2;

                for (int m = 0; m < motifSo.cells.Length; m++)
                {
                    MotifCellData mc = motifSo.cells[m];
                    (int tileIndex, TileSO tile) = shapeToTile[mc.shapeType];

                    Vector2 center = latticeBase + mc.localCenterUnit * cellScale;
                    center = QuantizeVec2(center, motifSo.positionQuantizeScale);

                    float rot = QuantizeFloat(mc.localRotationDeg, motifSo.rotationQuantizeScale);

                    long qx = (long)Mathf.Round(center.x * motifSo.positionQuantizeScale);
                    long qy = (long)Mathf.Round(center.y * motifSo.positionQuantizeScale);
                    int qr = Mathf.RoundToInt(rot * motifSo.rotationQuantizeScale);
                    var key = (qx, qy, mc.shapeType, qr);

                    if (placedKeys.Contains(key))
                    {
                        //continue;
                    }

                    Vector2[] verts = BuildWorldVertices(tile, center, rot, cellScale);
                    if (!PolygonIntersectsRect(verts, screenRect))
                    {
                        //continue;
                    }

                    Vector3 pos3 = new(center.x, center.y, placementZ);
                    view.AddPlaced(tile, pos3, rot, cellScale, placedRoot, latticeColorIndex, copyColor);
                    nextPlacedOrder++;

                    boundaryTracker.AddPlacedTile(tileIndex, center, rot);
                    placedKeys.Add(key);
                }
            }
        }

        if (boundaryTracker.PlacedTileCount == 0)
        {
            reason = "回铺后未生成任何单元。";
            return false;
        }

        return true;
    }

    private Vector2 ResolveRebuildOrigin(
        PeriodicMotifSO motifSo,
        List<PlacedTileData> anchorSnapshot,
        Rect screenRect)
    {
        // fallback：保持原有行为（屏幕中心）
        Vector2 fallback = new(
            (screenRect.xMin + screenRect.xMax) * 0.5f,
            (screenRect.yMin + screenRect.yMax) * 0.5f);
        fallback = QuantizeVec2(fallback, motifSo.positionQuantizeScale);

        if (anchorSnapshot == null || anchorSnapshot.Count == 0 || motifSo.cells == null || motifSo.cells.Length == 0)
        {
            return fallback;
        }

        int pairCount = Mathf.Min(anchorSnapshot.Count, motifSo.cells.Length);
        float rotTol = 2f / Mathf.Max(1f, motifSo.rotationQuantizeScale);

        for (int i = 0; i < pairCount; i++)
        {
            PlacedTileData placed = anchorSnapshot[i];
            if (placed.tileIndex < 0 || placed.tileIndex >= mainDataSO.tiles.Count)
            {
                continue;
            }

            TileSO tile = mainDataSO.tiles[placed.tileIndex];
            if (tile == null)
            {
                continue;
            }

            MotifCellData cell = motifSo.cells[i];
            if (cell.shapeType != tile.shapeType)
            {
                continue;
            }

            float qRot = QuantizeFloat(placed.rotationDeg, motifSo.rotationQuantizeScale);
            if (Mathf.Abs(Mathf.DeltaAngle(qRot, cell.localRotationDeg)) > rotTol)
            {
                continue;
            }

            Vector2 qPos = QuantizeVec2(placed.position, motifSo.positionQuantizeScale);
            Vector2 origin = qPos - cell.localCenterUnit * cellScale;
            return QuantizeVec2(origin, motifSo.positionQuantizeScale);
        }

        return fallback;
    }

    private static Vector2[] BuildWorldVertices(TileSO tile, Vector2 pos, float rotDeg, float scale)
    {
        Vector2[] src = tile.localVertices;
        int n = src.Length;
        Vector2[] dst = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            Vector2 p = src[i] * scale;
            dst[i] = PolygonSnapSolver.Rotate(p, rotDeg) + pos;
        }

        return dst;
    }

    private bool TryGetScreenRectOnPlacementPlane(out Rect rect)
    {
        rect = default;

        if (!TryScreenPointToPlane(new Vector2(0f, 0f), out Vector2 bl)
            || !TryScreenPointToPlane(new Vector2(0f, Screen.height), out Vector2 tl)
            || !TryScreenPointToPlane(new Vector2(Screen.width, 0f), out Vector2 br)
            || !TryScreenPointToPlane(new Vector2(Screen.width, Screen.height), out Vector2 tr))
        {
            return false;
        }

        float minX = Mathf.Min(bl.x, tl.x, br.x, tr.x);
        float maxX = Mathf.Max(bl.x, tl.x, br.x, tr.x);
        float minY = Mathf.Min(bl.y, tl.y, br.y, tr.y);
        float maxY = Mathf.Max(bl.y, tl.y, br.y, tr.y);

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private bool TryScreenPointToPlane(Vector2 screenPos, out Vector2 world2)
    {
        world2 = default;

        Ray ray = worldCamera.ScreenPointToRay(screenPos);
        Plane plane = new(Vector3.forward, new Vector3(0f, 0f, placementZ));
        if (!plane.Raycast(ray, out float enter))
        {
            return false;
        }

        Vector3 p = ray.GetPoint(enter);
        world2 = new Vector2(p.x, p.y);
        return true;
    }

    private static bool PolygonIntersectsRect(Vector2[] verts, Rect rect)
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

    private static bool IsPointInPolygon(Vector2 p, Vector2[] polygon)
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

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        const float eps = 1e-6f;
        if (Mathf.Abs(Cross(a, b, p)) > eps)
        {
            return false;
        }

        return p.x >= Mathf.Min(a.x, b.x) - eps && p.x <= Mathf.Max(a.x, b.x) + eps
            && p.y >= Mathf.Min(a.y, b.y) - eps && p.y <= Mathf.Max(a.y, b.y) + eps;
    }

    private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
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

    private void ClearPlacedTiles()
    {
        view.ClearPlaced();
        boundaryTracker.Clear();

        nextPlacedOrder = placedOrderStart;
        selectedSnapEdgeIndex = -1;
        detectedSnapEdgeIndex = -1;
        hasActiveBoundaryEdge = false;
        hasSnapSolution = false;
        previewCanPlace = false;
        isSnapLatched = false;
        placementLocked = false;
        ClearOverlapGizmoState();

        if (cachedMotifForExport != null)
        {
            Destroy(cachedMotifForExport);
            cachedMotifForExport = null;
        }

        if (currentTileIndex >= 0 && view.HasPreview)
        {
            view.SetPreviewVisual(true, previewAlpha, previewValidColor, previewInvalidColor);
        }
    }

#if UNITY_EDITOR
    private bool TryCreatePeriodicMotifAsset(
        PeriodicMotifSO so,
        out string assetPath,
        out string error)
    {
        assetPath = string.Empty;
        error = string.Empty;

        if (so == null)
        {
            error = "SO 为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(exportAssetFolder) || !exportAssetFolder.StartsWith("Assets"))
        {
            error = "exportAssetFolder 必须是 Assets 下路径。";
            return false;
        }

        EnsureFolder(exportAssetFolder);

        string safeName = SanitizeFileName("NewMotif");
        string rawPath = $"{exportAssetFolder}/{safeName}.asset";
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(rawPath);

        AssetDatabase.CreateAsset(so, uniquePath);
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = so;
        assetPath = uniquePath;
        return true;
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
        {
            name = name.Replace(invalid[i], '_');
        }

        return name;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
        {
            return;
        }

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
#endif

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

    private void ClearOverlapGizmoState()
    {
        gizmoHasOverlap = false;
        gizmoOverlapHandPolygon = null;
        gizmoOverlapPlacedPolygon = null;
    }

    private bool TryGetMouseWorldOnPlacementPlane(out Vector3 worldPos)
    {
        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, placementZ));

        if (!plane.Raycast(ray, out float enter))
        {
            worldPos = default;
            return false;
        }

        worldPos = ray.GetPoint(enter);
        worldPos.z = placementZ;
        return true;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}