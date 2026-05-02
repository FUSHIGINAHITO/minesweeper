using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Game : MonoBehaviour
{
    public static Game instance => _instance;
    public static Game _instance;

    public Map map;

    public MainDataSO so;

    // 按下/松开相关
    private Cell pressedCell;
    private Color pressedOriginalColor;

    // Chord 相关
    private bool leftChordActive;
    private Cell leftChordTarget;
    private HashSet<Cell> leftHighlighted = new();

    private bool rightChordActive;
    private Cell rightChordTarget;
    private HashSet<Cell> rightHighlighted = new();

    // 游戏状态
    private bool gameOver = false;

    // 计时与剩余雷数
    private int flaggedCount = 0;
    private float elapsedTime = 0f;
    private bool timerRunning = false;

    private void Awake()
    {
        _instance = this;
    }

    // 初始化地图与 UI
    private void Start()
    {
        map.Generate();

        foreach (var c in map.cellList)
        {
            c.image.color = so.defaultColor;
        }

        UIManager.instance.UpdateRestMine(map.totalMineCount - flaggedCount);
        UIManager.instance.UpdateTimer(elapsedTime);
        UIManager.instance.SetFaceNormal();
    }

    private void Update()
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

        // 计时器
        if (timerRunning)
        {
            elapsedTime += Time.deltaTime;
            UIManager.instance.UpdateTimer(elapsedTime);
        }

        var cam = UIManager.instance.mainCamera;

        // 检查 chord 结束
        if (leftChordActive && !Input.GetMouseButton(0))
        {
            DeactivateChord(true, false);
        }

        if (rightChordActive && !Input.GetMouseButton(1))
        {
            DeactivateChord(true, true);
        }

        // 右键处理：已显示数字则触发右键 chord，否则切换旗子（若未按左键）
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var hit2D = Physics2D.GetRayIntersection(ray);

            if (hit2D.collider != null)
            {
                var clicked = hit2D.collider.GetComponentInParent<Cell>();

                if (clicked != null)
                {
                    if (clicked.isShown && clicked.value > 0)
                    {
                        ActivateChord(clicked, true);
                    }
                    else
                    {
                        if (!Input.GetMouseButton(0))
                        {
                            ToggleFlag(clicked);
                        }
                    }
                }
            }
        }

        // 左键按下：开始计时或触发左键 chord，或标记按下格子
        if (Input.GetMouseButtonDown(0))
        {
            if (!timerRunning)
            {
                timerRunning = true;
                elapsedTime = 0f;
                UIManager.instance.UpdateTimer(elapsedTime);
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var hit2D = Physics2D.GetRayIntersection(ray);

            if (hit2D.collider != null)
            {
                var clicked = hit2D.collider.GetComponentInParent<Cell>();

                if (clicked != null)
                {
                    if (clicked.isShown && clicked.value > 0)
                    {
                        ActivateChord(clicked, false);
                    }
                    else if (!clicked.isShown && !clicked.isFlagged)
                    {
                        pressedCell = clicked;
                        pressedOriginalColor = pressedCell.image.color;
                        pressedCell.image.color = so.pressedColor;
                        UIManager.instance.SetFaceHold();
                    }
                }
            }
        }

        // 左键松开：首次点击触发布雷并揭开
        if (Input.GetMouseButtonUp(0))
        {
            if (pressedCell != null)
            {
                if (!map.minesPlaced)
                {
                    map.PlaceMinesAvoiding(pressedCell);
                    UIManager.instance.UpdateRestMine(map.totalMineCount - flaggedCount);
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
                    UIManager.instance.SetFaceNormal();
                }
            }
        }
    }

    // 激活 chord：高亮邻居并显示按下表情
    private void ActivateChord(Cell numberCell, bool isRightChord)
    {
        if (isRightChord)
        {
            if (rightChordActive)
            {
                return;
            }

            rightChordActive = true;
            rightChordTarget = numberCell;
            rightHighlighted.Clear();

            foreach (var n in numberCell.neighbours)
            {
                if (!n.isShown && n.isFlagged)
                {
                    n.image.color = so.chordColorFlag;
                    rightHighlighted.Add(n);
                }
            }
        }
        else
        {
            if (leftChordActive)
            {
                return;
            }

            leftChordActive = true;
            leftChordTarget = numberCell;
            leftHighlighted.Clear();

            foreach (var n in numberCell.neighbours)
            {
                if (!n.isShown && !n.isFlagged)
                {
                    n.image.color = so.chordColor;
                    leftHighlighted.Add(n);
                }
            }
        }

        UIManager.instance.SetFaceHold();
    }

    // 取消 chord：恢复高亮并根据需要自动标雷或扫开
    private void DeactivateChord(bool applyAutoFlag, bool isRightChord)
    {
        if (isRightChord)
        {
            if (!rightChordActive)
            {
                return;
            }

            // 恢复高亮颜色（跳过被另一侧高亮的格子）
            RestoreHighlightedColors(rightHighlighted, leftHighlighted);

            // 收集 targets
            var targets = GetUnshownNeighbors(rightChordTarget);

            // 自动标旗（如果条件满足）
            ApplyAutoFlagIfNeeded(rightChordTarget, targets, applyAutoFlag);

            // 若剩余地雷为 0，自动揭开安全格子（与左键一致）
            AutoRevealIfNoRemainingMines(rightChordTarget, targets);

            rightHighlighted.Clear();
            rightChordTarget = null;
            rightChordActive = false;
        }
        else
        {
            if (!leftChordActive)
            {
                return;
            }

            // 恢复高亮颜色（跳过被另一侧高亮的格子）
            RestoreHighlightedColors(leftHighlighted, rightHighlighted);

            // 收集 targets
            var targets = GetUnshownNeighbors(leftChordTarget);

            // 自动标旗（如果条件满足）
            ApplyAutoFlagIfNeeded(leftChordTarget, targets, applyAutoFlag);

            // 若剩余地雷为 0，自动揭开安全格子
            AutoRevealIfNoRemainingMines(leftChordTarget, targets);

            leftHighlighted.Clear();
            leftChordTarget = null;
            leftChordActive = false;
        }

        if (!leftChordActive && !rightChordActive && !gameOver)
        {
            UIManager.instance.SetFaceNormal();
        }
    }

    // 恢复高亮区的颜色（跳过已显示、为 null 或被另一侧高亮的格子）
    private void RestoreHighlightedColors(HashSet<Cell> highlighted, HashSet<Cell> otherHighlighted)
    {
        foreach (var n in highlighted)
        {
            if (n == null)
            {
                continue;
            }

            if (n.isShown)
            {
                continue;
            }

            if (otherHighlighted.Contains(n))
            {
                continue;
            }

            n.image.color = n.isFlagged ? so.flagColor : so.defaultColor;
        }
    }

    // 返回 chord 目标的所有未显示邻居
    private List<Cell> GetUnshownNeighbors(Cell chordTarget)
    {
        var list = new List<Cell>();

        if (chordTarget != null)
        {
            foreach (var n in chordTarget.neighbours)
            {
                if (!n.isShown)
                {
                    list.Add(n);
                }
            }
        }

        return list;
    }

    // 自动标旗（保留原有逻辑：targets.Count == chordTarget.value）
    private void ApplyAutoFlagIfNeeded(Cell chordTarget, List<Cell> targets, bool applyAutoFlag)
    {
        if (!applyAutoFlag || targets.Count == 0 || chordTarget == null)
        {
            return;
        }

        if (targets.Count == chordTarget.value)
        {
            foreach (var t in targets)
            {
                if (!t.isShown && !t.isFlagged)
                {
                    t.isFlagged = true;
                    t.image.color = so.flagColor;
                    flaggedCount++;
                }
            }

            UIManager.instance.UpdateRestMine(map.totalMineCount - flaggedCount);
        }
    }

    // 当剩余地雷为 0 时，自动揭开安全邻居
    private void AutoRevealIfNoRemainingMines(Cell chordTarget, List<Cell> targets)
    {
        if (targets.Count == 0 || chordTarget == null)
        {
            return;
        }

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

    private void RestorePressedColor(Cell cell)
    {
        if (cell.isFlagged)
        {
            cell.image.color = so.flagColor;
            return;
        }

        cell.image.color = pressedOriginalColor;
    }

    // 切换旗子并更新计数显示
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
            cell.image.color = so.flagColor;
            flaggedCount++;
            UIManager.instance.UpdateRestMine(map.totalMineCount - flaggedCount);
            return;
        }

        cell.image.color = so.defaultColor;
        flaggedCount--;
        UIManager.instance.UpdateRestMine(map.totalMineCount - flaggedCount);
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

        cell.image.color = cell.isMine ? so.mineColor : so.revealedColor;

        cell.text.gameObject.SetActive(true);
        cell.text.text = cell.isMine ? string.Empty : (cell.value > 0 ? cell.value.ToString() : string.Empty);
        cell.text.color = so.colors[Mathf.Clamp(cell.value, 0, so.colors.Length - 1)];

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

    // 游戏失败：展示雷并更新 UI
    private void GameOver(Cell exploded)
    {
        if (gameOver)
        {
            return;
        }

        DeactivateChord(false, true);
        DeactivateChord(false, false);

        if (pressedCell != null)
        {
            RestorePressedColor(pressedCell);
            pressedCell = null;
        }

        gameOver = true;
        timerRunning = false;

        foreach (var c in map.cellList)
        {
            if (c.isMine)
            {
                c.isShown = true;
                c.image.color = so.mineColor;
            }
            else
            {
                if (c.isFlagged && !c.isMine)
                {
                    c.image.color = so.wrongFlagColor;
                }
            }
        }

        exploded.image.color = so.bombMineColor;

        UIManager.instance.SetBackgroundColor(so.defeatColor);
        UIManager.instance.SetFaceDefeat();
    }

    // 检查胜利并更新 UI
    private void CheckWin()
    {
        foreach (var c in map.cellList)
        {
            if (!c.isMine && !c.isShown)
            {
                return;
            }
        }

        DeactivateChord(false, true);
        DeactivateChord(false, false);

        if (pressedCell != null)
        {
            RestorePressedColor(pressedCell);
            pressedCell = null;
        }

        gameOver = true;
        timerRunning = false;

        foreach (var c in map.cellList)
        {
            if (c.isMine && !c.isFlagged)
            {
                c.isFlagged = true;
                c.image.color = so.flagColor;
            }
            else if (!c.isMine && c.isFlagged)
            {
                c.image.color = so.wrongFlagColor;
            }
        }

        flaggedCount = 0;

        foreach (var c in map.cellList)
        {
            if (c.isFlagged)
            {
                flaggedCount++;
            }
        }

        UIManager.instance.UpdateRestMine(map.totalMineCount - flaggedCount);
        UIManager.instance.SetBackgroundColor(so.victoryColor);
        UIManager.instance.SetFaceVictory();
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}