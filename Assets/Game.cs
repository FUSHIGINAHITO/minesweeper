using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Game : MonoBehaviour
{
    public GameObject cellPrefab;
    public Color[] colors;

    public int width = 10;
    public int height = 10;
    public int mineCount = 10;

    private Cell[,] cells;

    private List<Cell> cellList = new();

    private bool minesPlaced = false;

    // 新增：记录按下时的格子和其原始颜色，用于按下变灰、松开恢复或触发
    private Cell pressedCell;
    private Color pressedOriginalColor;

    // 新增：左右键同时按（chord）状态（现在由左键单独触发）
    private bool chordActive;
    private Cell chordTarget;
    private Dictionary<Cell, Color> chordOriginalColors = new();

    // 新增：游戏结束标志
    private bool gameOver = false;

    void Start()
    {
        // 计算摄像机可见世界边界，确定每个格子的边长（单位为世界坐标）
        var cam = Camera.main;
        float cellSize = 1f;
        Vector3 origin = Vector3.zero;

        if (cam != null)
        {
            float camDistance = Mathf.Abs(cam.transform.position.z); // 假设格子在 z = 0 平面
            var bottomLeft = cam.ScreenToWorldPoint(new Vector3(0f, 0f, camDistance));
            var topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, camDistance));

            float worldWidth = topRight.x - bottomLeft.x;
            float worldHeight = topRight.y - bottomLeft.y;

            // 取能完整铺满屏幕的单元边长（保持正方形单元）
            cellSize = Mathf.Min(worldWidth / width, worldHeight / height);

            // 使用屏幕中心来计算起点，使整体网格居中
            var centerWorld = (bottomLeft + topRight) * 0.5f;
            origin = new Vector3(
                centerWorld.x - (width - 1) * cellSize * 0.5f,
                centerWorld.y - (height - 1) * cellSize * 0.5f,
                0f
            );
        }

        cells = new Cell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var position = origin + new Vector3(x * cellSize, y * cellSize, 0f);
                var obj = Instantiate(cellPrefab, position, Quaternion.identity);
                // 使用 scale 控制格子尺寸，默认格子边长为 1m，因此直接设为 cellSize
                obj.transform.localScale = new Vector3(cellSize, cellSize, 1f);

                var cell = obj.GetComponent<Cell>();
                cells[x, y] = cell;
                cellList.Add(cell);
                cell.i = x;
                cell.j = y;
            }
        }
    }


    void Update()
    {
        // 始终允许按 R 重置
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        // 游戏结束后不再响应其他输入
        if (gameOver) return;

        var cam = Camera.main;
        if (cam == null) return;

        // 现在：如果 chord 激活但用户松开左键 -> 结束 chord
        if (chordActive && !Input.GetMouseButton(0))
        {
            // 松开时不再判断鼠标是否仍在原格，直接按需处理自动标记/扫开
            DeactivateChord(true);
        }

        // 处理右键按下（切换旗子），只有在左键没有按住时才切换
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var hit2D = Physics2D.GetRayIntersection(ray);
            if (hit2D.collider != null)
            {
                var clicked = hit2D.collider.GetComponentInParent<Cell>();
                if (clicked != null)
                {
                    // 仅当左键未按住时，右键用于切换标记（保留原行为）
                    if (!Input.GetMouseButton(0))
                    {
                        ToggleFlag(clicked);
                    }
                }
            }
        }

        // 处理左键按下：
        // - 如果按在已展开的数字格上（value > 0），立即激活 chord（不再需要右键同时按）
        // - 否则（未展开且未标记）记录 pressedCell（变灰）
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            var hit2D = Physics2D.GetRayIntersection(ray);
            if (hit2D.collider != null)
            {
                var clicked = hit2D.collider.GetComponentInParent<Cell>();
                if (clicked != null)
                {
                    // 如果点击的是已展开且为数字的格子 -> 激活 chord（左键单独触发）
                    if (clicked.isShown && clicked.value > 0)
                    {
                        ActivateChord(clicked);
                    }
                    else if (!clicked.isShown && !clicked.isFlagged)
                    {
                        // 如果当前 chord 未激活且右键未按，记录 pressedCell
                        pressedCell = clicked;
                        if (pressedCell.image != null)
                        {
                            pressedOriginalColor = pressedCell.image.color;
                            pressedCell.image.color = Color.gray;
                        }
                    }
                }
            }
        }

        // 左键松开：无论鼠标是否仍在按下时的格子上都触发（当 chord 激活时，top 的逻辑会先结束 chord）
        if (Input.GetMouseButtonUp(0))
        {
            if (pressedCell != null)
            {
                // 始终在松开时触发（不再判断位置）
                if (!minesPlaced)
                {
                    PlaceMinesAvoiding(pressedCell);
                }

                if (pressedCell.isFlagged)
                {
                    RestorePressedColor(pressedCell);
                }
                else if (pressedCell.isMine)
                {
                    Reveal(pressedCell);
                    Debug.Log("Boom! 点击到地雷。");
                    // Reveal 会设置 gameOver 并展示所有地雷
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
        if (numberCell == null) return;
        if (chordActive) return;

        chordActive = true;
        chordTarget = numberCell;
        chordOriginalColors.Clear();

        // 收集需要变色的邻居（未展开且未标记）
        foreach (var n in numberCell.neighbours)
        {
            if (n != null && !n.isShown && !n.isFlagged)
            {
                if (n.image != null && !chordOriginalColors.ContainsKey(n))
                {
                    chordOriginalColors[n] = n.image.color;
                    n.image.color = Color.gray;
                }
            }
        }
    }

    private void DeactivateChord(bool applyAutoFlag)
    {
        if (!chordActive) return;

        // 恢复原色
        foreach (var kv in chordOriginalColors)
        {
            var cell = kv.Key;
            var col = kv.Value;
            if (cell != null && cell.image != null && !cell.isShown && !cell.isFlagged)
            {
                cell.image.color = col;
            }
        }

        // 收集目标（未展开）
        var targets = new List<Cell>();
        if (chordTarget != null)
        {
            foreach (var n in chordTarget.neighbours)
            {
                if (n != null && !n.isShown)
                {
                    targets.Add(n);
                }
            }
        }

        // 如果需要：检查是否全部为雷，若是则自动标上
        if (applyAutoFlag && targets.Count > 0)
        {
            bool allAreMines = true;
            foreach (var t in targets)
            {
                if (t.isFlagged) continue;
                if (!t.isMine)
                {
                    allAreMines = false;
                    break;
                }
            }

            if (allAreMines)
            {
                foreach (var t in targets)
                {
                    if (!t.isShown && !t.isFlagged)
                    {
                        t.isFlagged = true;
                        t.image.color = Color.yellow;
                    }
                }
            }
        }

        // 如果目标邻居中没有任何未标记的雷，则全部展开（扫开）
        if (targets.Count > 0)
        {
            bool anyMine = false;
            foreach (var t in targets)
            {
                if (t.isMine && !t.isFlagged)
                {
                    anyMine = true;
                    break;
                }
            }

            if (!anyMine)
            {
                foreach (var t in targets)
                {
                    if (gameOver) break;
                    if (!t.isShown && !t.isFlagged)
                    {
                        // 对每个安全格子执行递归展开，保证连通的0会被扩散展开
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
        if (cell == null || cell.image == null) return;
        // 标记状态优先显示标记颜色
        if (cell.isFlagged)
        {
            cell.image.color = Color.yellow;
            cell.text.gameObject.SetActive(true);
            cell.text.text = "F";
        }
        else
        {
            cell.image.color = pressedOriginalColor;
        }
    }

    private void ToggleFlag(Cell cell)
    {
        if (cell == null) return;
        if (gameOver) return;
        if (cell.isShown) return;

        cell.isFlagged = !cell.isFlagged;
        if (cell.isFlagged)
        {
            // 显示旗子
            if (cell.text != null)
            {
                cell.text.gameObject.SetActive(true);
                cell.text.text = "F";
            }
            if (cell.image != null)
            {
                cell.image.color = Color.yellow;
            }
        }
        else
        {
            // 取消旗子
            if (cell.text != null)
            {
                cell.text.gameObject.SetActive(false);
                cell.text.text = "";
            }
            if (cell.image != null)
            {
                cell.image.color = Color.white;
            }
        }
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

        // 从候选中移除首次点击的格子
        candidates.Remove(firstClicked);
        mineCount = Mathf.Min(mineCount, candidates.Count);

        Shuffle(candidates);
        for (int i = 0; i < mineCount; i++)
        {
            candidates[i].isMine = true;
        }

        minesPlaced = true;

        // 放完地雷后重新计算相邻地雷数
        CalculateNeighbours();
    }

    private void CalculateNeighbours()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
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

                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
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
        if (cell.isShown) return;
        if (cell.isFlagged) return; // 被标记的不能被展开

        cell.isShown = true;

        var sr = cell.image;
        sr.color = cell.isMine ? Color.red : Color.clear;

        if (cell.text != null)
        {
            cell.text.gameObject.SetActive(true);
            cell.text.text = cell.isMine ? "X" : (cell.value > 0 ? cell.value.ToString() : "");
            cell.text.color = colors[Mathf.Clamp(cell.value, 0, colors.Length - 1)];
        }

        if (cell.isMine)
        {
            // 触雷 -> 游戏结束
            GameOver(cell);
        }
    }

    private void RevealRecursive(Cell start)
    {
        var stack = new Stack<Cell>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            if (gameOver) return;

            var c = stack.Pop();
            if (c.isShown) continue;

            Reveal(c);

            if (gameOver) return;

            // 只有当当前格子没有相邻雷（value == 0）时，才继续展开邻居
            if (c.value == 0 && !c.isMine)
            {
                foreach (var n in c.neighbours)
                {
                    if (n != null && !n.isShown && !n.isMine && !n.isFlagged)
                    {
                        stack.Push(n);
                    }
                }
            }
        }
    }

    private void GameOver(Cell exploded)
    {
        if (gameOver) return;
        gameOver = true;

        // 展示所有地雷
        foreach (var c in cellList)
        {
            if (c == null) continue;

            if (c.isMine)
            {
                c.isShown = true;
                if (c.image != null)
                {
                    c.image.color = Color.red;
                }
                if (c.text != null)
                {
                    c.text.gameObject.SetActive(true);
                    c.text.text = "X";
                }
            }
            else
            {
                // 可选：显示被错误标记的格子（例如变色）
                if (c.isFlagged && !c.isMine)
                {
                    if (c.image != null)
                    {
                        c.image.color = new Color(0.5f, 0f, 0.5f); // 紫色表示错误标记
                    }
                }
            }
        }

        Debug.Log("Game Over. 按 R 重新开始。");
    }
}