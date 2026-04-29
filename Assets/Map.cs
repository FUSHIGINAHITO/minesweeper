using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Map : MonoBehaviour
{
    public GameObject cellPrefab;
    public float cellSize = 0.2f;

    // 雷率（总格子数 * mineRatio -> 地雷数）
    [Range(0f, 1f)]
    public float mineRatio = 0.2063f;

    // 屏幕边缘留白（百分比）
    [Range(0f, 0.5f)]
    public float marginLeftPercent = 0.01f;

    [Range(0f, 0.5f)]
    public float marginRightPercent = 0.01f;

    [Range(0f, 0.5f)]
    public float marginTopPercent = 0.05f;

    [Range(0f, 0.5f)]
    public float marginBottomPercent = 0.01f;

    // 运行时网格数据（供 Game 使用）
    [HideInInspector, NonSerialized]
    public Cell[,] cells;
    [HideInInspector, NonSerialized]
    public List<Cell> cellList = new();
    [HideInInspector, NonSerialized]
    public int totalMineCount;
    [HideInInspector, NonSerialized]
    public bool minesPlaced = false;

    // 生成网格并构建邻居关系（不放置地雷）
    public void Generate()
    {
        cellList.Clear();

        GenerateGrid();
        BuildNeighbours();

        totalMineCount = Mathf.RoundToInt(cellList.Count * mineRatio);
        minesPlaced = false;
    }

    abstract protected void GenerateGrid();

    // 为所有格子建立邻居列表
    abstract protected void BuildNeighbours();

    // 在第一次点击时放置地雷（避免 firstClicked）
    public void PlaceMinesAvoiding(Cell firstClicked)
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
        CalculateNeighbourValues();
    }

    // 计算每个格子的邻雷数（在放置地雷后调用）
    private void CalculateNeighbourValues()
    {
        foreach (var cell in cellList)
        {
            int adjacentMines = 0;

            foreach (var neighbour in cell.neighbours)
            {
                if (neighbour.isMine)
                {
                    adjacentMines++;
                }
            }

            cell.value = adjacentMines;
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
}