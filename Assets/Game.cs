using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
    public Map map;

    public Color[] colors;

    public TMP_Text restMine;
    public TMP_Text timer;
    public Image image;

    public Sprite normalSprite;
    public Sprite holdSprite;
    public Sprite victorySprite;
    public Sprite defeatSprite;

    // 格子颜色配置
    public Color defaultColor = Color.white;
    public Color pressedColor = Color.gray;
    public Color chordColor = Color.gray;
    public Color flagColor = Color.yellow;
    public Color revealedColor = Color.clear;
    public Color mineColor = Color.red;
    public Color bombMineColor = Color.red;
    public Color wrongFlagColor = new Color(0.5f, 0f, 0.5f);

    // 胜利背景颜色（可在 Inspector 中调整）
    public Color victoryColor = Color.green;
    public Color defeatColor = Color.red;

    private Cell[,] cells;
    private List<Cell> cellList;
    private bool minesPlaced = false;
    private int totalMineCount;

    // 按下/松开相关
    private Cell pressedCell;
    private Color pressedOriginalColor;

    // Chord（同时展开）相关
    private bool chordActive;
    private Cell chordTarget;
    private Dictionary<Cell, Color> chordOriginalColors = new Dictionary<Cell, Color>();

    // 游戏结束标志
    private bool gameOver = false;

    // 计时与剩余雷数
    private int flaggedCount = 0;
    private float elapsedTime = 0f;
    private bool timerRunning = false;

    void Start()
    {
        if (map == null)
        {
            Debug.LogError("Map is not assigned on Game.");
            return;
        }

        map.Generate();

        // 同步数据引用
        cells = map.cells;
        cellList = map.cellList;
        totalMineCount = map.totalMineCount;
        minesPlaced = map.minesPlaced;

        // 初始化格子颜色（由 Game 控制视觉样式）
        foreach (var c in cellList)
        {
            c.image.color = defaultColor;
        }

        // 初始化 UI
        UpdateRestMine();
        UpdateTimerText();

        image.sprite = normalSprite;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Restart();
            return;
        }

        if (gameOver)
        {
            return;
        }

        // 更新计时器
        if (timerRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerText();
        }

        var cam = Camera.main;

        if (chordActive && !Input.GetMouseButton(0))
        {
            DeactivateChord(true);
        }

        // 右键切换旗子（仅在未按左键时）
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var hit2D = Physics2D.GetRayIntersection(ray);
            if (hit2D.collider == null)
            {
                return;
            }
            var clicked = hit2D.collider.GetComponentInParent<Cell>();
            if (clicked == null)
            {
                return;
            }

            if (!Input.GetMouseButton(0))
            {
                ToggleFlag(clicked);
            }
        }

        // 左键按下：开始计时（如尚未开始），数字格触发 chord，否则标记为按下（变灰）
        if (Input.GetMouseButtonDown(0))
        {
            if (!timerRunning)
            {
                timerRunning = true;
                elapsedTime = 0f;
                UpdateTimerText();
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var hit2D = Physics2D.GetRayIntersection(ray);
            if (hit2D.collider == null)
            {
                return;
            }
            var clicked = hit2D.collider.GetComponentInParent<Cell>();
            if (clicked == null)
            {
                return;
            }

            if (clicked.isShown && clicked.value > 0)
            {
                ActivateChord(clicked);
            }
            else if (!clicked.isShown && !clicked.isFlagged)
            {
                pressedCell = clicked;
                pressedOriginalColor = pressedCell.image.color;
                pressedCell.image.color = pressedColor;

                image.sprite = holdSprite;
            }
        }

        // 左键松开：在按下位置触发操作（首次点击触发布雷）
        if (Input.GetMouseButtonUp(0))
        {
            if (pressedCell != null)
            {
                if (!minesPlaced)
                {
                    map.PlaceMinesAvoiding(pressedCell);

                    // 同步地图变化
                    totalMineCount = map.totalMineCount;
                    minesPlaced = map.minesPlaced;

                    UpdateRestMine();
                }

                if (pressedCell.isFlagged)
                {
                    RestorePressedColor(pressedCell);
                }
                else if (pressedCell.isMine)
                {
                    Reveal(pressedCell);
                }
                else
                {
                    RevealRecursive(pressedCell);
                }

                pressedCell = null;

                if (!gameOver)
                {
                    image.sprite = normalSprite;
                }
            }
        }
    }

    private void ActivateChord(Cell numberCell)
    {
        if (numberCell == null)
        {
            return;
        }

        if (chordActive)
        {
            return;
        }

        chordActive = true;
        chordTarget = numberCell;
        chordOriginalColors.Clear();

        foreach (var n in numberCell.neighbours)
        {
            if (!n.isShown && !n.isFlagged)
            {
                chordOriginalColors[n] = n.image.color;
                n.image.color = chordColor;
            }
        }

        image.sprite = holdSprite;
    }

    private void DeactivateChord(bool applyAutoFlag)
    {
        if (!chordActive)
        {
            return;
        }

        foreach (var kv in chordOriginalColors)
        {
            var cell = kv.Key;
            var col = kv.Value;
            if (!cell.isShown && !cell.isFlagged)
            {
                cell.image.color = col;
            }
        }

        var targets = new List<Cell>();

        foreach (var n in chordTarget.neighbours)
        {
            if (!n.isShown)
            {
                targets.Add(n);
            }
        }

        if (applyAutoFlag && targets.Count > 0)
        {
            // 只有在“周围未扫开的格数等于理论上的雷数”时才自动标雷
            if (targets.Count == chordTarget.value)
            {
                foreach (var t in targets)
                {
                    if (!t.isShown && !t.isFlagged)
                    {
                        t.isFlagged = true;
                        t.image.color = flagColor;
                        flaggedCount++;
                    }
                }

                UpdateRestMine();
            }
        }

        if (targets.Count > 0)
        {
            // 如果已标的雷数等于该数字的理论雷数，则尝试扫开剩下的未标邻居（可能触雷）
            int flaggedNeighbors = 0;
            foreach (var n in chordTarget.neighbours)
            {
                if (n.isFlagged)
                {
                    flaggedNeighbors++;
                }
            }

            int remainingMines = chordTarget.value - flaggedNeighbors;

            if (remainingMines == 0)
            {
                foreach (var t in targets)
                {
                    if (gameOver)
                    {
                        break;
                    }

                    if (!t.isShown && !t.isFlagged)
                    {
                        RevealRecursive(t);
                    }
                }
            }
        }

        chordOriginalColors.Clear();
        chordTarget = null;
        chordActive = false;

        if (!gameOver)
        {
            image.sprite = normalSprite;
        }
    }

    private void RestorePressedColor(Cell cell)
    {
        if (cell.isFlagged)
        {
            cell.image.color = flagColor;
            return;
        }

        cell.image.color = pressedOriginalColor;
    }

    private void ToggleFlag(Cell cell)
    {
        if (gameOver)
        {
            return;
        }

        if (cell.isShown)
        {
            return;
        }

        cell.isFlagged = !cell.isFlagged;

        if (cell.isFlagged)
        {
            cell.image.color = flagColor;
            flaggedCount++;
            UpdateRestMine();
            return;
        }

        cell.image.color = defaultColor;
        flaggedCount--;
        UpdateRestMine();
    }

    private void Reveal(Cell cell)
    {
        if (cell.isShown)
        {
            return;
        }

        if (cell.isFlagged)
        {
            return;
        }

        cell.isShown = true;

        cell.image.color = cell.isMine ? mineColor : revealedColor;

        cell.text.gameObject.SetActive(true);
        cell.text.text = cell.isMine ? string.Empty : (cell.value > 0 ? cell.value.ToString() : string.Empty);
        cell.text.color = colors[Mathf.Clamp(cell.value, 0, colors.Length - 1)];

        if (cell.isMine)
        {
            GameOver(cell);
            return;
        }

        CheckWin();
    }

    private void RevealRecursive(Cell start)
    {
        var stack = new Stack<Cell>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            if (gameOver)
            {
                return;
            }

            var c = stack.Pop();

            if (c.isShown)
            {
                continue;
            }

            Reveal(c);

            if (gameOver)
            {
                return;
            }

            if (c.value == 0 && !c.isMine)
            {
                foreach (var n in c.neighbours)
                {
                    if (!n.isShown && !n.isMine && !n.isFlagged)
                    {
                        stack.Push(n);
                    }
                }
            }
        }
    }

    private void GameOver(Cell exploded)
    {
        if (gameOver)
        {
            return;
        }

        gameOver = true;
        timerRunning = false;

        foreach (var c in cellList)
        {
            if (c.isMine)
            {
                c.isShown = true;
                c.image.color = mineColor;
            }
            else
            {
                if (c.isFlagged && !c.isMine)
                {
                    c.image.color = wrongFlagColor;
                }
            }
        }

        exploded.image.color = bombMineColor;
        Camera.main.backgroundColor = defeatColor;

        image.sprite = defeatSprite;
    }

    // 检查是否胜利：所有非雷格被扫开
    private void CheckWin()
    {
        foreach (var c in cellList)
        {
            if (!c.isMine && !c.isShown)
            {
                return;
            }
        }

        gameOver = true;
        timerRunning = false;
        Camera.main.backgroundColor = victoryColor;

        image.sprite = victorySprite;
    }

    // 更新剩余雷数显示（总雷数 - 已标记）
    private void UpdateRestMine()
    {
        restMine.text = (totalMineCount - flaggedCount).ToString();
    }

    // 更新计时器显示（秒）
    private void UpdateTimerText()
    {
        timer.text = Mathf.FloorToInt(elapsedTime).ToString();
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}