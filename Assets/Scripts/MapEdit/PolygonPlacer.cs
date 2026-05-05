using UnityEngine;
using UnityEngine.EventSystems;

public class PolygonPlacer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private PolygonTileCatalog catalog;
    [SerializeField] private Transform placedRoot;
    [SerializeField] private Transform previewRoot;

    [Header("Auto Buttons")]
    [SerializeField] private PolygonButton tileButtonPrefab;
    [SerializeField] private Transform tileButtonRoot;

    [Header("Placement Plane (2D XY)")]
    [SerializeField] private float placementZ = 0f;

    [Header("Cell Init")]
    [SerializeField] private float cellScale = 1f;
    [SerializeField] private int cellTypeId = -1;

    [Header("Preview")]
    [SerializeField, Range(0.05f, 1f)] private float previewAlpha = 0.45f;

    private PolygonTileCatalog.PolygonTileDefinition currentDef;
    private Cell previewCell;
    private float currentRotationDeg;

    private void Start()
    {
        BuildTileButtonsOnce();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelHolding();
        }

        if (previewCell == null)
        {
            return;
        }

        if (TryGetMouseWorldOnPlacementPlane(out Vector3 worldPos))
        {
            previewCell.transform.SetPositionAndRotation(
                worldPos,
                Quaternion.Euler(0f, 0f, currentRotationDeg));
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            PlaceCurrentAtPreview();
        }
    }

    private void BuildTileButtonsOnce()
    {
        for (int i = 0; i < catalog.Count; i++)
        {
            var def = catalog.Get(i);
            var btn = Instantiate(tileButtonPrefab, tileButtonRoot);
            int capturedIndex = i;
            btn.image.sprite = PoolManager.instance.GetSharedPolygonSprite(def.shapeType);
            btn.button.onClick.AddListener(() => SelectTileByIndex(capturedIndex));
            btn.name = $"TileButton_{capturedIndex}_{def.id}";
        }
    }

    public void SelectTileByIndex(int index)
    {
        currentDef = catalog.Get(index);
        currentRotationDeg = 0f;
        RebuildPreview();
    }

    public void SetCurrentRotationDeg(float deg)
    {
        currentRotationDeg = deg;

        if (previewCell != null)
        {
            previewCell.transform.rotation = Quaternion.Euler(0f, 0f, currentRotationDeg);
        }
    }

    public void CancelHolding()
    {
        currentDef = null;
        ReturnPreviewToPool();
    }

    private void RebuildPreview()
    {
        ReturnPreviewToPool();

        if (TryGetMouseWorldOnPlacementPlane(out Vector3 worldPos))
        {
            previewCell = RequireAndInitCell(currentDef, worldPos, currentRotationDeg);
        }
        else
        {
            previewCell = RequireAndInitCell(currentDef, new Vector3(0f, 0f, placementZ), currentRotationDeg);
        }

        previewCell.transform.SetParent(previewRoot, true);
        ApplyPreviewVisual(previewCell, true);
    }

    private void PlaceCurrentAtPreview()
    {
        if (currentDef == null || previewCell == null)
        {
            return;
        }

        Vector3 pos = previewCell.transform.position;
        float rotZ = previewCell.transform.eulerAngles.z;

        var placed = RequireAndInitCell(currentDef, pos, rotZ);
        placed.transform.SetParent(placedRoot, true);
        ApplyPreviewVisual(placed, false);
    }

    private Cell RequireAndInitCell(PolygonTileCatalog.PolygonTileDefinition def, Vector3 pos, float rotDeg)
    {
        var cell = PoolManager.instance.cellPool.Require();
        cell.Init(def.shapeType, pos, Quaternion.Euler(0f, 0f, rotDeg), cellScale, false, cellTypeId);
        cell.InitShowArt();
        return cell;
    }

    private void ReturnPreviewToPool()
    {
        if (previewCell == null)
        {
            return;
        }

        previewCell.Return();
        previewCell = null;
    }

    private void ApplyPreviewVisual(Cell cell, bool isPreview)
    {
        Color c = cell.image.color;
        c.a = isPreview ? previewAlpha : 1f;
        cell.image.color = c;
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