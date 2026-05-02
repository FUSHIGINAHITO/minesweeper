using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance => _instance;
    private static UIManager _instance;

    // UI 元素
    public Camera mainCamera;
    public TMP_Text restMine;
    public TMP_Text timer;
    public Image faceImage;

    private void Awake()
    {
        _instance = this;
    }

    // 更新剩余雷数显示
    public void UpdateRestMine(int remaining)
    {
        restMine.text = remaining.ToString();
    }

    // 更新计时显示（秒）
    public void UpdateTimer(float elapsedSeconds)
    {
        timer.text = Mathf.FloorToInt(elapsedSeconds).ToString();
    }

    // 表情：正常
    public void SetFaceNormal()
    {
        faceImage.sprite = Game.instance.so.normalSprite;
    }

    // 表情：按下
    public void SetFaceHold()
    {
        faceImage.sprite = Game.instance.so.holdSprite;
    }

    // 表情：胜利
    public void SetFaceVictory()
    {
        faceImage.sprite = Game.instance.so.victorySprite;
    }

    // 表情：失败
    public void SetFaceDefeat()
    {
        faceImage.sprite = Game.instance.so.defeatSprite;
    }

    // 设置背景色
    public void SetBackgroundColor(Color color)
    {
        mainCamera.backgroundColor = color;
    }
}