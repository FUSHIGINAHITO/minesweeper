using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MainDataSO", menuName = "Minesweeper/MainData", order = 0)]
public class MainDataSO : ScriptableObject
{
    public List<Sprite> polygonSprites = new();

    [Header("屏幕边缘留白（百分比）")]
    public float marginLeftPercent = 0.01f;
    public float marginRightPercent = 0.01f;
    public float marginTopPercent = 0.05f;
    public float marginBottomPercent = 0.01f;

    public Color[] colors;

    [Header("格子颜色配置")]
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

    [Header("格子贴图")]
    public Sprite normalSprite;
    public Sprite holdSprite;
    public Sprite victorySprite;
    public Sprite defeatSprite;

    [Header("界面背景色")]
    public Color normalBgColor;
    public Color victoryColor;
    public Color defeatColor;

    public float textSize;
}