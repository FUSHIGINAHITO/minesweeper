using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager instance => _instance;
    private static PoolManager _instance;

    public TextPool text;
    public CellPool triangle;
    public CellPool square;
    public CellPool hex;

    public enum CellShapeType
    {
        Triangle = 0,
        Square = 1,
        Hex = 2,
    }

    private readonly List<Vector2[]> sharedLocalVertices = new();

    private void Awake()
    {
        _instance = this;
        BuildSharedLocalVertices();
    }

    public IReadOnlyList<Vector2> GetSharedLocalVertices(CellShapeType shapeType)
    {
        return sharedLocalVertices[(int)shapeType];
    }

    private void BuildSharedLocalVertices()
    {
        const float side = 1f;
        float h = Mathf.Sqrt(3f) * 0.5f * side;

        // 正三角形：朝上（正上方是顶点），以质心为原点
        sharedLocalVertices.Add(new Vector2[]
        {
            new(0f, 2f * h / 3f),
            new(0.5f * side, -h / 3f),
            new(-0.5f * side, -h / 3f)
        });

        // 正方形：不旋转（正上方是边），以中心为原点
        sharedLocalVertices.Add(new Vector2[]
        {
            new(-0.5f * side, -0.5f * side),
            new(0.5f * side, -0.5f * side),
            new(0.5f * side, 0.5f * side),
            new(-0.5f * side, 0.5f * side)
        });

        // 正六边形：flat-top（正上方是边），以中心为原点
        sharedLocalVertices.Add(new Vector2[]
        {
            new(side, 0f),
            new(0.5f * side, h),
            new(-0.5f * side, h),
            new(-side, 0f),
            new(-0.5f * side, -h),
            new(0.5f * side, -h)
        });
    }
}