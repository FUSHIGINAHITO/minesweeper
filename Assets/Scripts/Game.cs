using System;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{
    public static Game instance => _instance;
    private static Game _instance;

    public bool debug;
    public MainDataSO so;
    public CellOverlayImageProjector projector;

    [HideInInspector, NonSerialized] public PeriodicMotifMap map;
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

    private readonly ChordData leftChord = new();
    private readonly ChordData rightChord = new();
    private Cell pressedCell;

    [HideInInspector, NonSerialized] public bool gameOver = false;
    [HideInInspector, NonSerialized] public int restMineCount = 0;
    [HideInInspector, NonSerialized] public float elapsedTime = 0f;

    private bool timerRunning = false;
    private readonly Stack<Cell> revealStack = new();
    private readonly List<Cell> tmp = new();

    private void Awake()
    {
        _instance = this;
        map = gameObject.AddComponent<PeriodicMotifMap>();
        ConfigureProjectorMaterialOptOut();
    }

    private void ConfigureProjectorMaterialOptOut()
    {
        HashSet<Material> disabled = new();

        for (int i = 0; i < so.tiles.Count; i++)
        {
            TileSO tile = so.tiles[i];
            //disabled.Add(tile.polygonMaterialOverride);
            disabled.Add(tile.polygonBorderMaterialOverride);
            disabled.Add(tile.polygonRevealedMaterialOverride);
        }

        projector.SetOverlayDisabledMaterials(disabled);
    }

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

        if (timerRunning)
        {
            elapsedTime += Time.deltaTime;
        }

        HandleMouseInput();
    }

    private void HandleMouseInput()
    {
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
                        TryToggleFlag(cell);
                    }
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            DeactivateChord(false);

            if (pressedCell != null)
            {
                if (!pressedCell.isFlagged)
                {
                    TryOpenCell(pressedCell);
                }

                pressedCell = null;
            }
        }

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
        if (chordData.active)
        {
            return;
        }

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

    private void DeactivateChord(bool isRightChord)
    {
        var chordData = isRightChord ? rightChord : leftChord;
        if (!chordData.active)
        {
            return;
        }

        foreach (var c in chordData.highlighted)
        {
            c.Restore();
        }

        var cell = chordData.target;
        var otherData = isRightChord ? leftChord : rightChord;
        if (!gameOver && !otherData.active)
        {
            TryChord(cell);
        }

        chordData.Deactivate();
    }

    // 统一操作 API：打开一个格子（包含首击布雷、连锁展开、胜负判定）
    public bool TryOpenCell(Cell cell)
    {
        if (cell == null || gameOver || cell.isRevealed || cell.isFlagged)
        {
            return false;
        }

        if (!map.minesPlaced)
        {
            map.PlaceMinesAvoiding(cell);
            timerRunning = true;
        }

        revealStack.Clear();
        revealStack.Push(cell);
        RevealFromStack();

        return true;
    }

    // 统一操作 API：切换旗子（用户右键）
    public bool TryToggleFlag(Cell cell)
    {
        if (cell == null || gameOver || cell.isRevealed)
        {
            return false;
        }

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

        return true;
    }

    // 统一操作 API：确定标旗（给 Bot 使用，避免 toggle）
    public bool TryFlagCell(Cell cell)
    {
        if (cell == null || gameOver || cell.isRevealed || cell.isFlagged)
        {
            return false;
        }

        cell.Flag();
        restMineCount--;
        return true;
    }

    // 统一操作 API：Chord（自动标旗 + 自动扫开）
    public bool TryChord(Cell chordTarget)
    {
        if (chordTarget == null || gameOver || !chordTarget.isRevealed || chordTarget.value <= 0)
        {
            return false;
        }

        chordTarget.GetUnshownNeighbors(tmp);
        if (tmp.Count == 0)
        {
            return false;
        }

        bool changed = false;

        if (tmp.Count == chordTarget.value)
        {
            foreach (var t in tmp)
            {
                if (!t.isRevealed && !t.isFlagged)
                {
                    t.Flag();
                    restMineCount--;
                    changed = true;
                }
            }
        }

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
            revealStack.Clear();
            foreach (var t in tmp)
            {
                if (!t.isRevealed && !t.isFlagged)
                {
                    revealStack.Push(t);
                }
            }

            if (revealStack.Count > 0)
            {
                RevealFromStack();
                changed = true;
            }
        }

        return changed;
    }

    private void RevealFromStack()
    {
        while (revealStack.Count > 0)
        {
            var c = revealStack.Pop();
            if (c.isRevealed || c.isFlagged)
            {
                continue;
            }

            c.Reveal();

            if (c.isMine)
            {
                Defeat(c);
                return;
            }

            CheckWin();
            if (gameOver)
            {
                return;
            }

            if (c.value == 0)
            {
                foreach (var n in c.neighbours)
                {
                    if (!n.isRevealed && !n.isFlagged)
                    {
                        revealStack.Push(n);
                    }
                }
            }
        }
    }

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

        map.Return();
        map.motifSO = so.periodicMotifs[curId];
        curId = (curId + 1) % so.periodicMotifs.Count;

        var maxMinSize = float.MinValue;
        var minMaxSize = float.MaxValue;
        foreach (var c in map.motifSO.cells)
        {
            var tileSO = so.GetTileSO(c.shapeType);
            maxMinSize = Mathf.Max(maxMinSize, tileSO.minSize);
            minMaxSize = Mathf.Min(minMaxSize, tileSO.maxSize);
        }

        map.Generate(maxMinSize);

        gameOver = false;
        restMineCount = map.totalMineCount;
        elapsedTime = 0f;
        timerRunning = false;

        UIManager.instance.GameStart();
    }
}