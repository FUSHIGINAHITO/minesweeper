using UnityEngine;
using UnityEngine.EventSystems;

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

    private readonly PolygonBoundaryTracker boundaryTracker = new();
    private readonly PolygonSnapSolver snapSolver = new();

    private int currentTileIndex = -1;
    private float heldRotationDeg;
    private float snappedRotationDeg;
    private Vector3 snappedPos;
    private bool hasSnapSolution;
    private bool previewCanPlace;

    private int selectedSnapEdgeIndex = -1;
    private bool hasActiveBoundaryEdge;
    private BoundaryEdge activeBoundaryEdge;
    private bool isSnapLatched;

    private Vector2 currentMouseWorld2D;
    private int nextPlacedOrder;

    // overlap gizmo
    private bool gizmoHasOverlap;
    private Vector2[] gizmoOverlapHandPolygon;
    private Vector2[] gizmoOverlapPlacedPolygon;

    private void Start()
    {
        nextPlacedOrder = placedOrderStart;
        BuildTileButtonsOnce();
    }

    private void Update()
    {
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
        for (int i = 0; i < mainDataSO.tiles.Count; i++)
        {
            TileSO tile = mainDataSO.tiles[i];
            PolygonButton btn = Instantiate(tileButtonPrefab, tileButtonRoot);
            int capturedIndex = i;

            btn.image.sprite = tile.polygonSprite;
            btn.button.onClick.AddListener(() => SelectTileByIndex(capturedIndex));
            btn.name = $"TileButton_{capturedIndex}_{tile.shapeType}";
        }
    }

    public void SelectTileByIndex(int index)
    {
        currentTileIndex = index;
        heldRotationDeg = 0f;
        snappedRotationDeg = 0f;
        selectedSnapEdgeIndex = -1;
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
            heldRotationDeg = rotDeg;
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
            ClearOverlapGizmoState();
        }
    }

    private void PlaceCurrent()
    {
        if (!hasSnapSolution || !previewCanPlace || currentTileIndex < 0)
        {
            return;
        }

        TileSO tile = mainDataSO.tiles[currentTileIndex];

        Vector2 qPos2 = QuantizeVec2(new Vector2(snappedPos.x, snappedPos.y), positionQuantizeScale);
        float qRot = QuantizeFloat(snappedRotationDeg, rotationQuantizeScale);

        Vector3 exactPos = new(qPos2.x, qPos2.y, placementZ);
        float exactRot = qRot;

        view.AddPlaced(tile, exactPos, exactRot, cellScale, placedRoot, nextPlacedOrder);
        nextPlacedOrder++;

        boundaryTracker.AddPlacedTile(currentTileIndex, qPos2, exactRot);

        isSnapLatched = false;
        selectedSnapEdgeIndex = -1;
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