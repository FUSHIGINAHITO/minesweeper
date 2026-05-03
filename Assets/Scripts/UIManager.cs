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

    private int _lastRestMine = int.MinValue;
    private int _lastTimer = int.MinValue;

    private void Awake()
    {
        _instance = this;
    }

    private void Update()
    {
        int currentRestMine = Game.instance.restMineCount;
        if (currentRestMine != _lastRestMine)
        {
            _lastRestMine = currentRestMine;
            restMine.text = currentRestMine.ToString();
        }

        int currentTimer = Mathf.FloorToInt(Game.instance.elapsedTime);
        if (currentTimer != _lastTimer)
        {
            _lastTimer = currentTimer;
            timer.text = currentTimer.ToString();
        }

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