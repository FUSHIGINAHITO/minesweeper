using System;
using UnityEngine;

[Serializable]
public struct MotifCellData
{
    [Header("Cell 类型")]
    public CellShapeType shapeType;

    [Header("局部中心（单位坐标，运行时会乘 s）")]
    public Vector2 localCenterUnit;

    [Header("局部旋转（度）")]
    public float localRotationDeg;

    [Header("typeId（可用于配色/分类）")]
    public int typeId;
}

[CreateAssetMenu(fileName = "PeriodicMotifSO", menuName = "Minesweeper/Map/Periodic Motif")]
public class PeriodicMotifSO : ScriptableObject
{
    [Header("基础信息")]
    public CellShapeType baselineShape = CellShapeType.Triangle;
    [Min(1)] public int shapeNum = 1;

    [Header("晶格基向量（单位坐标，运行时会乘 s）")]
    public Vector2 basis1Unit = Vector2.right;
    public Vector2 basis2Unit = Vector2.up;

    [Header("基元 Cell 列表（单位坐标，运行时会乘 s）")]
    public MotifCellData[] cells = Array.Empty<MotifCellData>();

    [Header("量化（用于导出/重建一致性）")]
    [Min(1f)] public float positionQuantizeScale = 100000f; // 1e-5
    [Min(1f)] public float rotationQuantizeScale = 10000f;  // 1e-4 deg

#if UNITY_EDITOR
    private void OnValidate()
    {
        shapeNum = Mathf.Max(1, shapeNum);

        if (cells == null)
        {
            cells = Array.Empty<MotifCellData>();
            return;
        }

        for (int i = 0; i < cells.Length; i++)
        {
            MotifCellData c = cells[i];
            c.localCenterUnit = QuantizeVec2(c.localCenterUnit, positionQuantizeScale);
            c.localRotationDeg = QuantizeFloat(c.localRotationDeg, rotationQuantizeScale);
            cells[i] = c;
        }

        basis1Unit = QuantizeVec2(basis1Unit, positionQuantizeScale);
        basis2Unit = QuantizeVec2(basis2Unit, positionQuantizeScale);
    }
#endif

    public Vector2 QuantizePosition(Vector2 v)
    {
        return QuantizeVec2(v, positionQuantizeScale);
    }

    public float QuantizeRotation(float deg)
    {
        return QuantizeFloat(deg, rotationQuantizeScale);
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