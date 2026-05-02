using System.Drawing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance => _instance;
    private static UIManager _instance;

    // UI ÔªËØ
    public Camera mainCamera;
    public TMP_Text restMine;
    public TMP_Text timer;
    public Image faceImage;

    private void Awake()
    {
        _instance = this;
    }

    private void Update()
    {
        var remaining = Game.instance.map.totalMineCount - Game.instance.flaggedCount;
        restMine.text = remaining.ToString();

        timer.text = Mathf.FloorToInt(Game.instance.elapsedTime).ToString();

        if (!Game.instance.gameOver)
        {
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                faceImage.sprite = Game.instance.so.holdSprite;
            }
            else
            {
                faceImage.sprite = Game.instance.so.normalSprite;
            }
        }
    }

    public void Victory()
    {
        faceImage.sprite = Game.instance.so.victorySprite;
        mainCamera.backgroundColor = Game.instance.so.victoryColor;
    }

    public void Defeat()
    {
        faceImage.sprite = Game.instance.so.defeatSprite;
        mainCamera.backgroundColor = Game.instance.so.defeatColor;
    }

    public void GameStart()
    {
        mainCamera.backgroundColor = Game.instance.so.normalBgColor;
    }
}