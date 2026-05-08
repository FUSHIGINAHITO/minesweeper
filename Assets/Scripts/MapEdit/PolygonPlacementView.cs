using System.Collections.Generic;
using UnityEngine;

public class PolygonPlacementView : MonoBehaviour
{
    [Header("Preview Animation")]
    [SerializeField, Min(0f)] private float previewMoveSmoothTime = 0.04f;
    [SerializeField, Min(0f)] private float previewRotateSmoothTime = 0.04f;
    [SerializeField, Min(0f)] private float previewScaleSmoothTime = 0.04f;
    [SerializeField, Min(0.01f)] private float previewMaxMoveSpeed = 100f;
    [SerializeField] private bool snapPreviewOnRebuild = true;

    private readonly List<Cell> placedCells = new();
    private Cell previewCell;
    private CellShapeType previewShapeType;

    private Vector3 previewTargetPos;
    private float previewTargetRotDeg;
    private float previewTargetScale = 1f;
    private int previewTargetSortingOrder;

    private Vector3 previewMoveVelocity;
    private float previewRotateVelocity;
    private float previewScaleVelocity;
    private bool previewPoseInitialized;

    public bool HasPreview => previewCell != null;

    private void LateUpdate()
    {
        UpdatePreviewPoseAnimation();
    }

    public void RebuildPreview(
        TileSO tile,
        Vector3 pos,
        float rotDeg,
        float scale,
        Transform parent,
        int sortingOrder)
    {
        ClearPreview();

        previewCell = RequireCell(tile, pos, rotDeg, scale);
        previewCell.transform.SetParent(parent, true);
        previewCell.image.sortingOrder = sortingOrder;
        previewShapeType = tile.shapeType;

        SetPreviewPose(pos, rotDeg, scale, sortingOrder);

        if (snapPreviewOnRebuild)
        {
            ApplyPreviewPoseImmediate();
        }
    }

    public void EnsurePreviewShape(
        TileSO tile,
        Vector3 pos,
        float rotDeg,
        float scale,
        Transform parent,
        int sortingOrder)
    {
        if (previewCell != null && previewShapeType == tile.shapeType)
        {
            return;
        }

        RebuildPreview(tile, pos, rotDeg, scale, parent, sortingOrder);
    }

    public void SetPreviewPose(Vector3 pos, float rotDeg, float scale, int sortingOrder)
    {
        if (previewCell == null)
        {
            return;
        }

        previewTargetPos = pos;
        previewTargetRotDeg = rotDeg;
        previewTargetScale = scale;
        previewTargetSortingOrder = sortingOrder;

        if (!previewPoseInitialized)
        {
            ApplyPreviewPoseImmediate();
        }
    }

    public void SetPreviewVisual(bool canPlace, float alpha, Color validColor, Color invalidColor)
    {
        if (previewCell == null)
        {
            return;
        }

        Color c = canPlace ? validColor : invalidColor;
        c.a = alpha;
        previewCell.image.color = c;
    }

    public void AddPlaced(
    TileSO tile,
    Vector3 pos,
    float rotDeg,
    float scale,
    Transform parent,
    int value,
    Color? tintColor = null)
    {
        Cell cell = RequireCell(tile, pos, rotDeg, scale);
        cell.transform.SetParent(parent, true);

        if (tintColor.HasValue)
        {
            Color c = tintColor.Value;
            c.a = 0.5f;
            cell.ShowEditArt(c, value);
        }

        placedCells.Add(cell);
    }

    public void ClearPreview()
    {
        if (previewCell == null)
        {
            return;
        }

        previewCell.ReturnAll();
        previewCell = null;
        previewPoseInitialized = false;
        previewMoveVelocity = Vector3.zero;
        previewRotateVelocity = 0f;
        previewScaleVelocity = 0f;
    }

    public void ClearPlaced()
    {
        for (int i = 0; i < placedCells.Count; i++)
        {
            placedCells[i].ReturnAll();
        }

        placedCells.Clear();
    }

    private void UpdatePreviewPoseAnimation()
    {
        if (previewCell == null || !previewPoseInitialized)
        {
            return;
        }

        Transform t = previewCell.transform;

        Vector3 pos = previewMoveSmoothTime <= 0f
            ? previewTargetPos
            : Vector3.SmoothDamp(t.position, previewTargetPos, ref previewMoveVelocity, previewMoveSmoothTime, previewMaxMoveSpeed);

        float currentRot = t.eulerAngles.z;
        float rot = previewRotateSmoothTime <= 0f
            ? previewTargetRotDeg
            : Mathf.SmoothDampAngle(currentRot, previewTargetRotDeg, ref previewRotateVelocity, previewRotateSmoothTime);

        float currentScale = t.localScale.x;
        float scale = previewScaleSmoothTime <= 0f
            ? previewTargetScale
            : Mathf.SmoothDamp(currentScale, previewTargetScale, ref previewScaleVelocity, previewScaleSmoothTime);

        t.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, rot));
        t.localScale = Vector3.one * scale;
        previewCell.image.sortingOrder = previewTargetSortingOrder;
    }

    private void ApplyPreviewPoseImmediate()
    {
        if (previewCell == null)
        {
            return;
        }

        previewCell.transform.SetPositionAndRotation(previewTargetPos, Quaternion.Euler(0f, 0f, previewTargetRotDeg));
        previewCell.transform.localScale = Vector3.one * previewTargetScale;
        previewCell.image.sortingOrder = previewTargetSortingOrder;

        previewMoveVelocity = Vector3.zero;
        previewRotateVelocity = 0f;
        previewScaleVelocity = 0f;
        previewPoseInitialized = true;
    }

    private static Cell RequireCell(TileSO tile, Vector3 pos, float rotDeg, float scale)
    {
        Cell cell = PoolManager.instance.cellPool.Require();
        cell.Init(tile.shapeType, pos, Quaternion.Euler(0f, 0f, rotDeg), scale, false, -1);
        cell.ShowEditArt(0.5f * Color.white, 0);
        return cell;
    }
}