using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance => _instance;
    private static UIManager _instance;

    // UI 元素
    public Camera mainCamera;
    public TMP_Text restMine;
    public TMP_Text timer;

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
    }

    public void Victory()
    {

    }

    public void Defeat()
    {

    }

    public void GameStart()
    {
        mainCamera.backgroundColor = Game.instance.so.normalBgColor;
    }
}