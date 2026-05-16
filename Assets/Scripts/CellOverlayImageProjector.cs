using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public sealed class CellOverlayImageProjector : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private bool enableOverlay = true;
    [SerializeField] private Texture2D overlayTexture;
    [SerializeField, Range(0f, 1f)] private float overlayStrength = 1f;
    [SerializeField] private Color overlayTint = Color.white;

    [Header("Bounds")]
    [SerializeField] private bool includeBorderCells = true;
    [SerializeField] private bool refreshEveryFrame = true;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.2f;

    [Header("Material Opt-Out")]
    [SerializeField] private bool enableMaterialOptOut = true;
    [SerializeField] private List<Material> overlayDisabledMaterials = new();

    private static readonly int CellOverlayTexId = Shader.PropertyToID("_CellOverlayTex");
    private static readonly int CellOverlayRectMinSizeId = Shader.PropertyToID("_CellOverlayRectMinSize");
    private static readonly int CellOverlayTintId = Shader.PropertyToID("_CellOverlayTint");
    private static readonly int CellOverlayParamsId = Shader.PropertyToID("_CellOverlayParams");

    private readonly HashSet<Material> runtimeMaterials = new();
    private float nextRefreshTime = -1f;

    private void OnEnable()
    {
        RefreshNow();
    }

    private void OnDisable()
    {
        DisableOverlayGlobals();
        RestoreRuntimeMaterialParams();
    }

    private void OnValidate()
    {
        RefreshNow();
    }

    private void Update()
    {
        if (!refreshEveryFrame || !Application.isPlaying)
        {
            return;
        }

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshNow();
    }

    [ContextMenu("Refresh Overlay Now")]
    public void RefreshNow()
    {
        if (!enableOverlay || overlayTexture == null)
        {
            DisableOverlayGlobals();
            ApplyMaterialOptOut(Vector4.zero);
            return;
        }

        if (!TryComputeCellWorldRect(out Vector2 rectMin, out Vector2 rectSize))
        {
            DisableOverlayGlobals();
            ApplyMaterialOptOut(Vector4.zero);
            return;
        }

        rectSize.x = Mathf.Max(rectSize.x, 1e-4f);
        rectSize.y = Mathf.Max(rectSize.y, 1e-4f);

        Shader.SetGlobalTexture(CellOverlayTexId, overlayTexture);
        Shader.SetGlobalVector(CellOverlayRectMinSizeId, new Vector4(rectMin.x, rectMin.y, rectSize.x, rectSize.y));
        Shader.SetGlobalColor(CellOverlayTintId, overlayTint);

        Vector4 enabledParams = new Vector4(1f, Mathf.Clamp01(overlayStrength), 0f, 0f);
        Shader.SetGlobalVector(CellOverlayParamsId, enabledParams);

        ApplyMaterialOptOut(enabledParams);
    }

    private bool TryComputeCellWorldRect(out Vector2 rectMin, out Vector2 rectSize)
    {
        rectMin = default;
        rectSize = default;

        if (Game.instance == null || Game.instance.map == null || Game.instance.map.allCellList == null)
        {
            return false;
        }

        var cells = Game.instance.map.allCellList;

        bool hasAny = false;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            if (cell == null || cell.image == null)
            {
                continue;
            }

            if (!includeBorderCells && cell.isBorder)
            {
                continue;
            }

            if (!cell.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds b = cell.image.bounds;

            if (b.min.x < minX)
            {
                minX = b.min.x;
            }

            if (b.min.y < minY)
            {
                minY = b.min.y;
            }

            if (b.max.x > maxX)
            {
                maxX = b.max.x;
            }

            if (b.max.y > maxY)
            {
                maxY = b.max.y;
            }

            hasAny = true;
        }

        if (!hasAny)
        {
            return false;
        }

        rectMin = new Vector2(minX, minY);
        rectSize = new Vector2(maxX - minX, maxY - minY);
        return true;
    }

    private void ApplyMaterialOptOut(Vector4 enabledParams)
    {
        if (!enableMaterialOptOut)
        {
            RestoreRuntimeMaterialParams();
            return;
        }

        runtimeMaterials.Clear();

        if (Game.instance == null || Game.instance.map == null || Game.instance.map.allCellList == null)
        {
            return;
        }

        var cells = Game.instance.map.allCellList;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            if (cell == null || cell.image == null)
            {
                continue;
            }

            Material mat = cell.image.sharedMaterial;
            if (mat == null)
            {
                continue;
            }

            if (!runtimeMaterials.Add(mat))
            {
                continue;
            }

            bool disableOnThisMat = overlayDisabledMaterials != null && overlayDisabledMaterials.Contains(mat);
            mat.SetVector(CellOverlayParamsId, disableOnThisMat ? Vector4.zero : enabledParams);
        }
    }

    private void RestoreRuntimeMaterialParams()
    {
        if (runtimeMaterials.Count == 0)
        {
            return;
        }

        foreach (Material mat in runtimeMaterials)
        {
            if (mat == null)
            {
                continue;
            }

            mat.SetVector(CellOverlayParamsId, new Vector4(1f, 1f, 0f, 0f));
        }

        runtimeMaterials.Clear();
    }

    private static void DisableOverlayGlobals()
    {
        Shader.SetGlobalTexture(CellOverlayTexId, Texture2D.whiteTexture);
        Shader.SetGlobalVector(CellOverlayRectMinSizeId, new Vector4(0f, 0f, 1f, 1f));
        Shader.SetGlobalColor(CellOverlayTintId, Color.white);
        Shader.SetGlobalVector(CellOverlayParamsId, Vector4.zero);
    }

    public void SetOverlayDisabledMaterials(IEnumerable<Material> materials)
    {
        overlayDisabledMaterials.Clear();

        if (materials == null)
        {
            return;
        }

        HashSet<Material> dedup = new();
        foreach (Material mat in materials)
        {
            if (mat == null)
            {
                continue;
            }

            if (dedup.Add(mat))
            {
                overlayDisabledMaterials.Add(mat);
            }
        }

        // 让材质禁用列表立即生效
        RefreshNow();
    }
}