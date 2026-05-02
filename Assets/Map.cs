using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Map : MonoBehaviour
{
    public float cellSize = 0.2f;

    // 雷率（总格子数 * mineRatio -> 地雷数）
    [Range(0f, 1f)]
    public float mineRatio = 0.2063f;

    [HideInInspector, NonSerialized]
    public Cell[,] cells;
    [HideInInspector, NonSerialized]
    public List<Cell> cellList = new();
    [HideInInspector, NonSerialized]
    public int totalMineCount;
    [HideInInspector, NonSerialized]
    public bool minesPlaced = false;

    protected Vector3 bottomLeft;
    protected Vector3 topRight;

    protected float worldWidth;
    protected float worldHeight;

    // 生成网格并构建邻居关系（不放置地雷）
    public void Generate()
    {
        var cam = UIManager.instance.mainCamera;
        var so = Game.instance.so;

        float camDistance = Mathf.Abs(cam.transform.position.z);

        float ml = Mathf.Clamp01(so.marginLeftPercent);
        float mr = Mathf.Clamp01(so.marginRightPercent);
        float mt = Mathf.Clamp01(so.marginTopPercent);
        float mb = Mathf.Clamp01(so.marginBottomPercent);

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

        bottomLeft = cam.ScreenToWorldPoint(new Vector3(Screen.width * ml, Screen.height * mb, camDistance));
        topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width * (1f - mr), Screen.height * (1f - mt), camDistance));

        worldWidth = topRight.x - bottomLeft.x;
        worldHeight = topRight.y - bottomLeft.y;

        cellList.Clear();

        GenerateGrid();
        BuildNeighbours();

        totalMineCount = Mathf.Min(Mathf.RoundToInt(cellList.Count * mineRatio), cellList.Count - 1);
        minesPlaced = false;

        Shuffle(cellList);
    }

    abstract protected void GenerateGrid();

    // 为所有格子建立邻居列表
    abstract protected void BuildNeighbours();

    // 在第一次点击时放置地雷（避免 firstClicked）
    public void PlaceMinesAvoiding(Cell firstClicked)
    {
        for (int i = 0; i < totalMineCount; i++)
        {
            cellList[i].isMine = true;
        }

        if (firstClicked.isMine)
        {
            firstClicked.isMine = false;
            cellList[totalMineCount].isMine = true;
        }

        minesPlaced = true;
        CalculateNeighbourValues();
    }

    // 计算每个格子的邻雷数（在放置地雷后调用）
    private void CalculateNeighbourValues()
    {
        foreach (var cell in cellList)
        {
            foreach (var neighbour in cell.neighbours)
            {
                if (neighbour.isMine)
                {
                    cell.value++;
                }
            }
        }
    }

    // Fisher–Yates shuffle
    private void Shuffle(List<Cell> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[j], list[i]) = (list[i], list[j]);
        }
    }

    public bool Win()
    {
        foreach (var c in cellList)
        {
            if (!c.isMine && !c.isRevealed)
            {
                return false;
            }
        }

        return true;
    }

    public void ShowRestMines(Cell exploded)
    {
        var so = Game.instance.so;
        foreach (var c in cellList)
        {
            if (c.isMine && !c.isFlagged)
            {
                c.isRevealed = true;
                c.image.color = so.mineColor;
            }
            else if (c.isFlagged && !c.isMine)
            {
                c.image.color = so.wrongFlagColor;
            }
        }

        exploded.image.color = so.bombMineColor;
    }

    public void FlagRestMines()
    {
        foreach (var c in cellList)
        {
            if (c.isMine && !c.isFlagged)
            {
                c.Flag();
            }
        }
    }

    public void Return()
    {
        foreach (var cell in cellList)
        {
            cell.Return();
        }
    }
}