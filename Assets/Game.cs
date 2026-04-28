using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Game : MonoBehaviour
{
    public GameObject cellPrefab;
    public float cellSize = 0.2f;
    public Color[] colors;

    public TMP_Text restMine;
    public TMP_Text timer;

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

    // 雷率（总格子数 * mineRatio -> 地雷数）
    [Range(0f, 1f)]
    public float mineRatio = 0.1f;

    // 屏幕边缘留白（百分比）
    [Range(0f, 0.5f)]
    public float marginLeftPercent = 0.05f;

    [Range(0f, 0.5f)]
    public float marginRightPercent = 0.05f;

    [Range(0f, 0.5f)]
    public float marginTopPercent = 0.05f;

    [Range(0f, 0.5f)]
    public float marginBottomPercent = 0.05f;

    private Cell[,] cells;
    private List<Cell> cellList = new List<Cell>();
    private bool minesPlaced = false;

    // 运行时网格尺寸（由屏幕尺寸计算）
    private int gridWidth;
    private int gridHeight;
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
        // 计算可用世界区域并尽量用 1m 单元填满（保持单元边长为 1）
        var cam = Camera.main;
        Vector3 origin = Vector3.zero;

        {
            float camDistance = Mathf.Abs(cam.transform.position.z);

            float ml = Mathf.Clamp01(marginLeftPercent);
            float mr = Mathf.Clamp01(marginRightPercent);
            float mt = Mathf.Clamp01(marginTopPercent);
            float mb = Mathf.Clamp01(marginBottomPercent);

            if (ml + mr >= 0.99f)
            {
                float excess = (ml + mr - 0.99f) * 0.5f;
                ml = Mathf.Max(0f, ml - excess);
                mr = Mathf.Max(0f, mr - excess);
            }

            if (mt + mb >= 0.99f)
            {
                float excess = (mt + mb - 0.99f) * 0.5f;
                mt = Mathf.Max(0f, mt - excess);
                mb = Mathf.Max(0f, mb - excess);
            }

            var bottomLeft = cam.ScreenToWorldPoint(new Vector3(Screen.width * ml, Screen.height * mb, camDistance));
            var topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width * (1f - mr), Screen.height * (1f - mt), camDistance));

            float worldWidth = topRight.x - bottomLeft.x;
            float worldHeight = topRight.y - bottomLeft.y;

            // 以 1m 为单元，尽量铺满可用区域
            gridWidth = Mathf.Max(1, Mathf.FloorToInt(worldWidth / cellSize));
            gridHeight = Mathf.Max(1, Mathf.FloorToInt(worldHeight / cellSize));

            var centerWorld = (bottomLeft + topRight) * 0.5f;
            origin = new Vector3(
                centerWorld.x - (gridWidth - 1) * cellSize * 0.5f,
                centerWorld.y - (gridHeight - 1) * cellSize * 0.5f,
                0f
            );
        }

        // 计算地雷总数
        totalMineCount = Mathf.RoundToInt(gridWidth * gridHeight * mineRatio);
        cells = new Cell[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                var position = origin + new Vector3(x * cellSize, y * cellSize, 0f);
                var obj = Instantiate(cellPrefab, position, Quaternion.identity);

                obj.transform.localScale = cellSize * Vector3.one;

                var cell = obj.GetComponent<Cell>();
                cells[x, y] = cell;
                cellList.Add(cell);
                cell.i = x;
                cell.j = y;

                cell.image.color = defaultColor;
            }
        }

        // 初始化 UI
        UpdateRestMine();
        UpdateTimerText();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
            }
        }

        // 左键松开：在按下位置触发操作（首次点击触发布雷）
        if (Input.GetMouseButtonUp(0))
        {
            if (pressedCell != null)
            {
                if (!minesPlaced)
                {
                    PlaceMinesAvoiding(pressedCell);
                    UpdateRestMine();
                }

                if (pressedCell.isFlagged)
                {
                    RestorePressedColor(pressedCell);
                }
                else if (pressedCell.isMine)
                {
                    Reveal(pressedCell);
                    Debug.Log("触雷。");
                }
                else
                {
                    RevealRecursive(pressedCell);
                }

                pressedCell = null;
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

    private void Shuffle(List<Cell> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[j], list[i]) = (list[i], list[j]);
        }
    }

    private void PlaceMinesAvoiding(Cell firstClicked)
    {
        var candidates = new List<Cell>(cellList);

        candidates.Remove(firstClicked);
        totalMineCount = Mathf.Min(totalMineCount, candidates.Count);

        Shuffle(candidates);

        for (int i = 0; i < totalMineCount; i++)
        {
            candidates[i].isMine = true;
        }

        minesPlaced = true;
        CalculateNeighbours();
    }

    private void CalculateNeighbours()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                var cell = cells[x, y];
                cell.neighbours.Clear();
                int adjacentMines = 0;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }

                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight)
                        {
                            var neighbour = cells[nx, ny];
                            cell.neighbours.Add(neighbour);

                            if (neighbour.isMine)
                            {
                                adjacentMines++;
                            }
                        }
                    }
                }

                cell.value = adjacentMines;
            }
        }
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
    }

    // 检查是否胜利：所有非雷格被扫开
    private void CheckWin()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                var c = cells[x, y];
                if (!c.isMine && !c.isShown)
                {
                    return;
                }
            }
        }

        gameOver = true;
        timerRunning = false;
        Camera.main.backgroundColor = victoryColor;
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
}
