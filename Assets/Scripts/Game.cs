using System;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{
    public static Game instance => _instance;
    private static Game _instance;

    public bool debug;
    public MainDataSO so;

    private Map[] maps;
    [HideInInspector, NonSerialized] public Map map;
    private int curId;

    private class ChordData
    {
        public bool active;
        public Cell target;
        public List<Cell> highlighted = new();

        public void Activate(Cell cell)
        {
            active = true;
            target = cell;
        }

        public void Deactivate()
        {
            active = false;
            target = null;
            highlighted.Clear();
        }
    }
    private ChordData leftChord = new();
    private ChordData rightChord = new();
    private Cell pressedCell;

    [HideInInspector, NonSerialized] public bool gameOver = false;
    [HideInInspector, NonSerialized] public int restMineCount = 0;
    [HideInInspector, NonSerialized] public float elapsedTime = 0f;
    private bool timerRunning = false;
    private Stack<Cell> stack = new();
    private List<Cell> tmp = new();

    private void Awake()
    {
        _instance = this;

        maps = GetComponentsInChildren<Map>();
        Cell.so = so;
    }

    // 初始化地图与 UI
    private void Start()
    {
        Restart();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Restart();
            return;
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            GameOver();
            map.Cheat();
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
        }

        // 左键按下
        if (Input.GetMouseButtonDown(0))
        {
            if (rightChord.active)
            {
                ActivateChord(rightChord.target, false);
            }
            else
            {
                var cell = GetCurCell();
                if (cell != null)
                {
                    if (cell.isRevealed)
                    {
                        ActivateChord(cell, false);
                    }
                    else if (!cell.isFlagged)
                    {
                        pressedCell = cell;
                        pressedCell.Pressed();
                    }
                }
            }
        }

        // 右键按下
        if (Input.GetMouseButtonDown(1))
        {
            if (leftChord.active)
            {
                ActivateChord(leftChord.target, true);
            }
            else
            {
                var cell = pressedCell != null ? pressedCell : GetCurCell();
                if (cell != null)
                {
                    if (cell.isRevealed)
                    {
                        ActivateChord(cell, true);
                    }
                    else
                    {
                        cell.ToggleFlag();
                        if (cell.isFlagged)
                        {
                            restMineCount--;
                        }
                        else
                        {
                            if (cell == pressedCell)
                            {
                                cell.Pressed();
                            }

                            restMineCount++;
                        }
                    }
                }
            }
        }

        // 左键松开
        if (Input.GetMouseButtonUp(0))
        {
            DeactivateChord(false);

            if (pressedCell != null)
            {
                if (!pressedCell.isFlagged)
                {
                    if (!map.minesPlaced)
                    {
                        map.PlaceMinesAvoiding(pressedCell);
                        timerRunning = true;
                    }

                    stack.Clear();
                    stack.Push(pressedCell);
                    RevealRecursive();
                }

                pressedCell = null;
            }
        }

        // 右键松开
        if (Input.GetMouseButtonUp(1))
        {
            DeactivateChord(true);
        }
    }

    private Cell GetCurCell()
    {
        var cam = UIManager.instance.mainCamera;
        float camDistance = Mathf.Abs(cam.transform.position.z);
        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(
            Input.mousePosition.x,
            Input.mousePosition.y,
            camDistance
        ));

        if (map.TryGetCellAtWorld(new Vector2(mouseWorld.x, mouseWorld.y), out var cell))
        {
            return cell;
        }

        return null;
    }

    private void ActivateChord(Cell cell, bool isRightChord)
    {
        var chordData = isRightChord ? rightChord : leftChord;
        if (!chordData.active)
        {
            if (pressedCell != null)
            {
                pressedCell.Restore();
                pressedCell = null;
            }

            chordData.Activate(cell);
            chordData.highlighted.Add(cell);

            foreach (var n in cell.neighbours)
            {
                if (!n.isRevealed && n.isFlagged == isRightChord)
                {
                    chordData.highlighted.Add(n);
                }
            }

            foreach (var c in chordData.highlighted)
            {
                c.Chord();
            }
        }
    }

    // 取消 chord：恢复高亮并根据需要自动标雷或扫开
    private void DeactivateChord(bool isRightChord)
    {
        var chordData = isRightChord ? rightChord : leftChord;
        if (chordData.active)
        {
            // 恢复高亮颜色
            foreach (var c in chordData.highlighted)
            {
                c.Restore();
            }

            var cell = chordData.target;
            cell.GetUnshownNeighbors(tmp);

            var otherData = isRightChord ? leftChord : rightChord;
            if (!gameOver && !otherData.active)
            {
                ApplyAutoFlagIfNeeded(cell, tmp);
                AutoRevealIfNoRemainingMines(cell, tmp);
            }

            chordData.Deactivate();
        }
    }

    // 自动标旗
    private void ApplyAutoFlagIfNeeded(Cell chordTarget, List<Cell> targets)
    {
        if (targets.Count == chordTarget.value)
        {
            foreach (var t in targets)
            {
                if (!t.isRevealed && !t.isFlagged)
                {
                    t.Flag();
                    restMineCount--;
                }
            }
        }
    }

    // 当剩余地雷为 0 时，自动揭开未标雷邻居
    private void AutoRevealIfNoRemainingMines(Cell chordTarget, List<Cell> targets)
    {
        int flaggedNeighbors = 0;
        foreach (var n in chordTarget.neighbours)
        {
            if (n.isFlagged)
            {
                flaggedNeighbors++;
            }
        }

        if (chordTarget.value == flaggedNeighbors)
        {
            stack.Clear();
            foreach (var t in targets)
            {
                if (!t.isRevealed && !t.isFlagged)
                {
                    stack.Push(t);
                }
            }
            RevealRecursive();
        }
    }

    private void RevealRecursive()
    {
        while (stack.Count > 0)
        {
            var c = stack.Pop();
            if (!c.isRevealed)
            {
                c.Reveal();

                if (c.isMine)
                {
                    Defeat(c);
                }
                else
                {
                    CheckWin();
                }

                if (gameOver)
                {
                    return;
                }

                if (c.value == 0 && !c.isMine)
                {
                    foreach (var n in c.neighbours)
                    {
                        if (!n.isRevealed && !n.isFlagged)
                        {
                            stack.Push(n);
                        }
                    }
                }
            }
        }
    }

    // 游戏失败：展示雷并更新 UI
    private void Defeat(Cell exploded)
    {
        if (gameOver)
        {
            return;
        }

        GameOver();

        map.ShowRestMines(exploded);

        UIManager.instance.Defeat();
    }

    private void CheckWin()
    {
        if (gameOver || !map.Win())
        {
            return;
        }

        GameOver();

        map.FlagRestMines();
        map.ShowVictoryAnim();
        restMineCount = 0;

        UIManager.instance.Victory();
    }

    public void GameOver()
    {
        gameOver = true;
        timerRunning = false;

        DeactivateChord(true);
        DeactivateChord(false);
        pressedCell = null;
    }

    public void Restart()
    {
        DeactivateChord(true);
        DeactivateChord(false);
        pressedCell = null;

        if (map != null)
        {
            map.Return();
        }

        map = maps[curId];
        curId = (curId + 1) % maps.Length;
        map.Generate();

        gameOver = false;
        restMineCount = map.totalMineCount;
        elapsedTime = 0;
        timerRunning = false;

        UIManager.instance.GameStart();
    }
}