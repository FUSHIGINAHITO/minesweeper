using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MainDataSO", menuName = "Minesweeper/MainData", order = 0)]
public class MainDataSO : ScriptableObject
{
    public List<Sprite> polygonSprites = new();
    public List<Texture> polygonSDFTextures = new();
    public List<Sprite> polygonShrinkSprites = new();
    public Material polygonBaseMaterial;
    public Material polygonRevealedMaterial;

    [Header("屏幕边缘留白（百分比）")]
    public float marginLeftPercent = 0.01f;
    public float marginRightPercent = 0.01f;
    public float marginTopPercent = 0.05f;
    public float marginBottomPercent = 0.01f;

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
    public float bevelSize;

    [Header("程序化连续配色（仅改变色相）")]
    [Range(0f, 1f)] public float hueSaturation = 0.9f;
    [Range(0f, 1f)] public float hueValue = 1f;
    [Range(0f, 1f)] public float hueAlpha = 1f;

    public Color[] GenerateHueCycleColors(int hueColorCount)
    {
        if (hueColorCount < 1)
        {
            hueColorCount = 1;
        }

        var hueCycleColors = new Color[hueColorCount];
        float hueStep = 1f / hueColorCount;

        // 随机起始色相
        float startHue = Random.value;
        // 随机顺/逆时针遍历色环
        bool reverse = Random.value > 0.5f;

        for (int i = 0; i < hueColorCount; i++)
        {
            float hue = reverse ? startHue - i * hueStep : startHue + i * hueStep;
            hue = hue - Mathf.Floor(hue); // 归一化到 [0,1)

            Color color = Color.HSVToRGB(hue, hueSaturation, hueValue);
            color.a = hueAlpha;
            hueCycleColors[i] = color;
        }

        return hueCycleColors;
    }
}