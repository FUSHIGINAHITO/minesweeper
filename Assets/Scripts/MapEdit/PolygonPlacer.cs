using UnityEngine;
using UnityEngine.EventSystems;

public class PolygonPlacer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private MainDataSO mainDataSO;
    [SerializeField] private Transform placedRoot;
    [SerializeField] private Transform previewRoot;

    [Header("Auto Buttons")]
    [SerializeField] private PolygonButton tileButtonPrefab;
    [SerializeField] private Transform tileButtonRoot;

    [Header("Placement Plane (2D XY)")]
    [SerializeField] private float placementZ = 0f;

    [Header("Preview")]
    [SerializeField, Range(0.05f, 1f)] private float previewAlpha = 0.45f;

    private TileSO currentTile;
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
        currentTile = mainDataSO.tiles[index];
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
        currentTile = null;
        ReturnPreviewToPool();
    }

    private void RebuildPreview()
    {
        ReturnPreviewToPool();

        Vector3 pos = new Vector3(0f, 0f, placementZ);
        if (TryGetMouseWorldOnPlacementPlane(out Vector3 worldPos))
        {
            pos = worldPos;
        }

        previewCell = RequireAndInitCell(currentTile, pos, currentRotationDeg);
        previewCell.transform.SetParent(previewRoot, true);
        ApplyPreviewVisual(previewCell, true);
    }

    private void PlaceCurrentAtPreview()
    {
        if (currentTile == null || previewCell == null)
        {
            return;
        }

        Vector3 pos = previewCell.transform.position;
        float rotZ = previewCell.transform.eulerAngles.z;

        Cell placed = RequireAndInitCell(currentTile, pos, rotZ);
        placed.transform.SetParent(placedRoot, true);
        ApplyPreviewVisual(placed, false);
    }

    private Cell RequireAndInitCell(TileSO tile, Vector3 pos, float rotDeg)
    {
        Cell cell = PoolManager.instance.cellPool.Require();
        cell.Init(tile.shapeType, pos, Quaternion.Euler(0f, 0f, rotDeg), 1f, false, -1);
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