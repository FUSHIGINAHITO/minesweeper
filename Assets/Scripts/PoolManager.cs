using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager instance => _instance;
    private static PoolManager _instance;

    public TextPool textPool;
    public CellPool cellPool;

    private readonly List<Vector2[]> sharedLocalVertices = new();

    private void Awake()
    {
        _instance = this;
        BuildSharedLocalVertices();
    }

    public Cell RequireCell(CellShapeType cellShapeType, Vector3 pos, Quaternion rot, float scale)
    {
        var cell = cellPool.Require();
        cell.Init(cellShapeType, pos, rot, scale);
        return cell;
    }

    public IReadOnlyList<Vector2> GetSharedLocalVertices(CellShapeType shapeType)
    {
        return sharedLocalVertices[(int)shapeType];
    }

    private void BuildSharedLocalVertices()
    {
        sharedLocalVertices.Clear();

        for (int n = 3; n <= 12; n++)
        {
            sharedLocalVertices.Add(BuildRegularPolygonVertices(n, 1f));
        }
    }

    private static Vector2[] BuildRegularPolygonVertices(int n, float size)
    {
        if (n < 3)
        {
            return System.Array.Empty<Vector2>();
        }

        var vertices = new Vector2[n];
        float step = Mathf.PI * 2f / n;

        // 边长 s -> 外接圆半径 R
        float radius = size / (2f * Mathf.Sin(Mathf.PI / n));

        // 保证“正下方是边”：-90° 对应某条边的中点方向
        float startAngle = -Mathf.PI * 0.5f - step * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float angle = startAngle + step * i;
            vertices[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return vertices;
    }
}