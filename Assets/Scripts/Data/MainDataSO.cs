using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MainDataSO", menuName = "Minesweeper/MainData", order = 0)]
public class MainDataSO : ScriptableObject
{
    public List<TileSO> tiles = new();
    public List<PeriodicMotifSO> periodicMotifs = new();

    public Material polygonBaseMaterial;
    public Material polygonBorderMaterial;
    public Material polygonRevealedMaterial;
    public float textSize;
    public float bevelSize;
    public float minCellSize;
    public float maxCellSize;

    [Header("屏幕边缘留白（统一厚度百分比，基于屏幕高度）")]
    public float marginTopPercent = 0.05f;

    public Color[] colors;

    [Header("格子颜色配置")]
    public Color borderColor;
    public Color defaultColor;
    public Color pressedColor;
    public Color chordColor;
    public Color chordColorFlag;
    public Color chordColorRevealed;
    public Color flagColor;
    public Color revealedColor;
    public Color mineColor;
    public Color bombMineColor;
    public Color wrongFlagColor;

    [Header("界面背景色")]
    public Color normalBgColor;

    [Header("程序化连续配色(OKLCH)")]
    [Range(0f, 1f)] public float lightness = 0.72f;
    [Range(0f, 1f)] public float chroma = 0.16f;
    [Range(0f, 1f)] public float alpha = 1f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizeTiles();
    }
#endif

    [ContextMenu("Normalize Tiles")]
    private void NormalizeTiles()
    {
        if (tiles == null)
        {
            tiles = new List<TileSO>();
            return;
        }

        var map = new Dictionary<CellShapeType, TileSO>();

        for (int i = 0; i < tiles.Count; i++)
        {
            TileSO tile = tiles[i];
            if (tile == null)
            {
                continue;
            }

            // 去重：保留第一个
            if (!map.ContainsKey(tile.shapeType))
            {
                map.Add(tile.shapeType, tile);
            }
        }

        tiles = new List<TileSO>(map.Values);
        tiles.Sort((a, b) => a.shapeType.CompareTo(b.shapeType));
    }

    public TileSO GetTileSO(CellShapeType shape)
    {
        return tiles[(int)shape];
    }
}