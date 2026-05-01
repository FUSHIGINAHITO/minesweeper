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
    public Color defaultColor;
    public Color pressedColor;
    public Color chordColor;
    public Color chordColorFlag;
    public Color flagColor;
    public Color revealedColor;
    public Color mineColor;
    public Color bombMineColor;
    public Color wrongFlagColor;

    // 胜利背景颜色（可在 Inspector 中调整）
    public Color victoryColor;
    public Color defeatColor;

    // 按下/松开相关
    private Cell pressedCell;
    private Color pressedOriginalColor;

    // Chord（左右独立）相关
    private bool leftChordActive;
    private Cell leftChordTarget;
    private HashSet<Cell> leftHighlighted = new HashSet<Cell>();

    private bool rightChordActive;
    private Cell rightChordTarget;
    private HashSet<Cell> rightHighlighted = new HashSet<Cell>();

    // 游戏结束标志
    private bool gameOver = false;

    // 计时与剩余雷数
    private int flaggedCount = 0;
    private float elapsedTime = 0f;
    private bool timerRunning = false;

    private void Start()
    {
        map.Generate();

        // 初始化格子颜色（由 Game 控制视觉样式）
        foreach (var c in map.cellList)
        {
            c.image.color = defaultColor;
        }

        // 初始化 UI
        UpdateRestMine();
        UpdateTimerText();

        image.sprite = normalSprite;
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

        // 更新计时器
        if (timerRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerText();
        }

        var cam = Camera.main;

        // 分别检测左右键对应的 chord 是否应该结束
        if (leftChordActive && !Input.GetMouseButton(0))
        {
            DeactivateChord(true, false); // 左键 chord 结束：保留自动标雷行为
        }
        if (rightChordActive && !Input.GetMouseButton(1))
        {
            DeactivateChord(false, true); // 右键 chord 结束：不触发自动标雷
        }

        // 右键：在数字格上触发右键 chord（高亮已标雷），否则切换旗子（仅在未按左键时）
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

            // 重要：如果点击的是已显示的数字格，总是触发右键 chord（即使左键被按住）
            if (clicked.isShown && clicked.value > 0)
            {
                ActivateChord(clicked, true); // 右键 chord
            }
            else
            {
                // 非数字格时仍然只在左键未按住时切换旗子（避免与左键冲突）
                if (!Input.GetMouseButton(0))
                {
                    ToggleFlag(clicked);
                }
            }
        }

        // 左键按下：开始计时（如尚未开始），数字格触发 chord（高亮未标雷），否则标记为按下（变灰）
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
                ActivateChord(clicked, false); // 左键 chord
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
                if (!map.minesPlaced)
                {
                    map.PlaceMinesAvoiding(pressedCell);

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

    // isRightChord: true = 右键 chord（高亮已标雷且未显示），false = 左键 chord（高亮未标雷且未显示）
    private void ActivateChord(Cell numberCell, bool isRightChord)
    {
        if (numberCell == null)
        {
            return;
        }

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
                    n.image.color = chordColorFlag;
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
                    n.image.color = chordColor;
                    leftHighlighted.Add(n);
                }
            }
        }

        // 如果任意一侧 chord 激活则显示按下表情
        image.sprite = holdSprite;
    }

    // applyAutoFlag：是否执行自动标雷与后续扫开；isRightChord 指明释放哪一侧 chord
    private void DeactivateChord(bool applyAutoFlag, bool isRightChord)
    {
        if (isRightChord)
        {
            if (!rightChordActive)
            {
                return;
            }

            // 恢复右侧高亮的格子（但如果左侧仍在高亮，则保留左侧颜色）
            foreach (var n in rightHighlighted)
            {
                if (n == null)
                {
                    continue;
                }
                if (n.isShown)
                {
                    continue;
                }
                if (leftHighlighted.Contains(n))
                {
                    // 左侧仍在高亮，跳过恢复
                    continue;
                }
                n.image.color = n.isFlagged ? flagColor : defaultColor;
            }

            var targets = new List<Cell>();
            if (rightChordTarget != null)
            {
                foreach (var n in rightChordTarget.neighbours)
                {
                    if (!n.isShown)
                    {
                        targets.Add(n);
                    }
                }
            }

            // 右键 chord 默认不启用自动标雷（传入的 applyAutoFlag 通常为 false）
            if (applyAutoFlag && targets.Count > 0)
            {
                if (targets.Count == rightChordTarget.value)
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

            // 恢复左侧高亮的格子（但如果右侧仍在高亮，则保留右侧颜色）
            foreach (var n in leftHighlighted)
            {
                if (n == null)
                {
                    continue;
                }
                if (n.isShown)
                {
                    continue;
                }
                if (rightHighlighted.Contains(n))
                {
                    // 右侧仍在高亮，跳过恢复
                    continue;
                }
                n.image.color = n.isFlagged ? flagColor : defaultColor;
            }

            var targets = new List<Cell>();
            if (leftChordTarget != null)
            {
                foreach (var n in leftChordTarget.neighbours)
                {
                    if (!n.isShown)
                    {
                        targets.Add(n);
                    }
                }
            }

            if (applyAutoFlag && targets.Count > 0)
            {
                // 只有在“周围未扫开的格数等于理论上的雷数”时才自动标雷
                if (targets.Count == leftChordTarget.value)
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
                if (leftChordTarget != null)
                {
                    foreach (var n in leftChordTarget.neighbours)
                    {
                        if (n.isFlagged)
                        {
                            flaggedNeighbors++;
                        }
                    }
                }

                int remainingMines = (leftChordTarget != null ? leftChordTarget.value : 0) - flaggedNeighbors;

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

            leftHighlighted.Clear();
            leftChordTarget = null;
            leftChordActive = false;
        }

        // 若任意 chord 仍在激活则保持按下表情，否则恢复正常表情
        if (!leftChordActive && !rightChordActive && !gameOver)
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

        foreach (var c in map.cellList)
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
        foreach (var c in map.cellList)
        {
            if (!c.isMine && !c.isShown)
            {
                return;
            }
        }

        gameOver = true;
        timerRunning = false;

        // 胜利时把所有雷都标为旗子并更新显示
        foreach (var c in map.cellList)
        {
            if (c.isMine && !c.isFlagged)
            {
                c.isFlagged = true;
                c.image.color = flagColor;
            }
            else if (!c.isMine && c.isFlagged)
            {
                // 如果之前误标的旗子，保持其颜色为 wrongFlagColor（可按需修改）
                c.image.color = wrongFlagColor;
            }
        }

        // 重新计算已标记数并刷新剩余雷数显示
        flaggedCount = 0;
        foreach (var c in map.cellList)
        {
            if (c.isFlagged)
            {
                flaggedCount++;
            }
        }
        UpdateRestMine();

        Camera.main.backgroundColor = victoryColor;

        image.sprite = victorySprite;
    }

    // 更新剩余雷数显示（总雷数 - 已标记）
    private void UpdateRestMine()
    {
        restMine.text = (map.totalMineCount - flaggedCount).ToString();
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