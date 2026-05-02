using UnityEngine;

[CreateAssetMenu(fileName = "MainDataSO", menuName = "Minesweeper/MainData", order = 0)]
public class MainDataSO : ScriptableObject
{
    public Color[] colors;

    [Header("格子颜色配置")]
    public Color defaultColor;
    public Color pressedColor;
    public Color chordColor;
    public Color chordColorFlag;
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
    public Color victoryColor;
    public Color defeatColor;
}