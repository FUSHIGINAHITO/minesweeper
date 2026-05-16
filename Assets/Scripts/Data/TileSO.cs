using UnityEngine;

[CreateAssetMenu(fileName = "TileSO", menuName = "Minesweeper/Tile", order = 1)]
public class TileSO : ScriptableObject
{
    [Header("基础")]
    public CellShapeType shapeType;

    [Header("渲染资源")]
    public Sprite polygonSprite;
    public Sprite polygonShrinkSprite;
    public Texture polygonSDFTexture;

    [Header("材质模板")]
    public Material polygonMaterialOverride;
    public Material polygonRevealedMaterialOverride;
    public Material polygonBorderMaterialOverride;

    [Header("参数覆盖")]
    public float bevelSize;

    [Header("预计算几何（比例是相对于正三角形）")]
    public float inradiusRatio;
    public float outradiusRatio;
    public float areaRatio;
    public Vector2[] localVertices;
    public float minSize;
    public float maxSize;
}