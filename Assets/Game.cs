using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{
    public GameObject cellPrefab;

    public int width = 10;
    public int height = 10;
    public int mineCount = 10;

    private Cell[,] cells;

    private List<Cell> cellList = new();

    private bool minesPlaced = false;

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
        // 左键点击展开格子（使用射线检测）
        if (Input.GetMouseButtonDown(0))
        {
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            Cell clicked = null;

            // 优先使用 2D 射线交点
            var hit2D = Physics2D.GetRayIntersection(ray);
            if (hit2D.collider != null)
            {
                clicked = hit2D.collider.GetComponentInParent<Cell>();
            }

            if (clicked == null) return;

            // 第一次点击时再随机布雷，确保点击的格子和其邻居安全
            if (!minesPlaced)
            {
                PlaceMinesAvoiding(clicked);
            }

            if (clicked.isMine)
            {
                // 点击到地雷：显示并结束（这里只做显示）
                Reveal(clicked);
                Debug.Log("Boom! 点击到地雷。");
            }
            else
            {
                // 非地雷：递归展开（使用栈实现）
                RevealRecursive(clicked);
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
        cell.isShown = true;

        var sr = cell.image;
        sr.color = cell.isMine ? Color.red : Color.clear;

        cell.text.gameObject.SetActive(true);
        cell.text.text = cell.isMine ? "X" : (cell.value > 0 ? cell.value.ToString() : "");
    }

    private void RevealRecursive(Cell start)
    {
        var stack = new Stack<Cell>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var c = stack.Pop();
            if (c.isShown) continue;

            Reveal(c);

            // 只有当当前格子没有相邻雷（value == 0）时，才继续展开邻居
            if (c.value == 0 && !c.isMine)
            {
                foreach (var n in c.neighbours)
                {
                    if (n != null && !n.isShown && !n.isMine)
                    {
                        stack.Push(n);
                    }
                }
            }
        }
    }
}