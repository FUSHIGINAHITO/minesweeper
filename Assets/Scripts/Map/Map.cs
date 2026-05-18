using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Map : MonoBehaviour
{
    public float cellSize;

    /// <summary>
    /// 密铺中内接圆最小的cell的类型
    /// </summary>
    public abstract CellShapeType BaselineShape
    {
        get;
    }

    /// <summary>
    /// 密铺中使用的形状的种数
    /// </summary>
    public abstract int ShapeNum
    {
        get;
    }

    [HideInInspector, NonSerialized] public float textSize;
    [HideInInspector, NonSerialized] public Color[] cellColorList;

    [Range(0f, 1f)]
    public float mineRatio = 0.2063f;
    [HideInInspector, NonSerialized] public List<Cell> cellList = new();
    [HideInInspector, NonSerialized] public List<Cell> allCellList = new();
    [HideInInspector, NonSerialized] public int totalMineCount;
    [HideInInspector, NonSerialized] public bool minesPlaced = false;

    protected Vector3 bottomLeft;
    protected Vector3 topRight;

    protected float worldWidth;
    protected float worldHeight;

    public void Generate(float size)
    {
        cellSize = size;
        var cam = UIManager.instance.mainCamera;
        var so = Game.instance.so;

        var areaRatio = so.GetTileSO(BaselineShape).areaRatio;
        var s = Mathf.Sqrt(areaRatio) * cellSize;
        textSize = s * so.textSize;
        cellColorList = ColorGenerator.GeneratePerceptualHueCycleColors(so, ShapeNum);

        float camDistance = Mathf.Abs(cam.transform.position.z);

        float m = Mathf.Clamp01(so.marginTopPercent);

        // 上下直接用高度百分比
        float mt = Mathf.Min(m, 0.495f);
        float mb = mt;

        // 左右换算成“等像素厚度”对应的宽度百分比
        float h2w = (float)Screen.height / Screen.width;
        float ml = Mathf.Min(mt * h2w, 0.495f);
        float mr = ml;

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

        GenerateGrid();
#if UNITY_EDITOR
        if (Game.instance.debug)
        {
            ValidateNoCellOverlapInEditor();
        }
#endif
        BuildNeighbours();

        totalMineCount = Mathf.Min(Mathf.RoundToInt(cellList.Count * mineRatio), cellList.Count - 1);
        minesPlaced = false;

        Shuffle(cellList);

        foreach (var cell in allCellList)
        {
            cell.InitShowArt();
        }
    }

    protected abstract void GenerateGrid();
    protected abstract void ValidateNoCellOverlapInEditor();
    protected abstract void BuildNeighbours();

    // 新增：无碰撞体拾取
    public abstract bool TryGetCellAtWorld(Vector2 worldPos, out Cell cell);

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
                c.Reveal();
            }
            else if (c.isFlagged && !c.isMine)
            {
                c.Unflag();
                c.Reveal();
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
        foreach (var cell in allCellList)
        {
            cell.ReturnAll();
        }

        cellList.Clear();
        allCellList.Clear();
    }

    public void ShowVictoryAnim()
    {
        foreach (var cell in allCellList)
        {
            cell.ShowColor();
        }
    }

    public void Cheat()
    {
        foreach (var cell in cellList)
        {
            cell.ShowAns();
        }
    }
}