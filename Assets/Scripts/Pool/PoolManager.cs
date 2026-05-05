using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager instance => _instance;
    private static PoolManager _instance;

    public TextPool textPool;
    public CellPool cellPool;

    [SerializeField] private MainDataSO mainDataSO;

    private readonly List<Vector2[]> sharedLocalVertices = new();
    private readonly List<float> sharedInradiusRatios = new();
    private readonly List<float> sharedAreaRatios = new();
    private readonly List<Material> sharedPolygonMaterials = new();
    private readonly List<Material> sharedPolygonBorderMaterials = new();

    private const int ShapeCount = 10; // 3~12 边形
    private static readonly int SdfTexId = Shader.PropertyToID("_SDFTex");
    private static readonly int BevelWidthId = Shader.PropertyToID("_BevelWidth");

    private void Awake()
    {
        _instance = this;
        BuildSharedLocalVertices();
        BuildSharedInradiusRatios();
        BuildSharedAreaRatios();
        BuildSharedPolygonMaterials();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < sharedPolygonMaterials.Count; i++)
        {
            Destroy(sharedPolygonMaterials[i]);
        }

        sharedPolygonMaterials.Clear();

        for (int i = 0; i < sharedPolygonBorderMaterials.Count; i++)
        {
            Destroy(sharedPolygonBorderMaterials[i]);
        }

        sharedPolygonBorderMaterials.Clear();
    }

    public Cell RequireCell(CellShapeType cellShapeType, Vector3 pos, Quaternion rot, float scale, bool isBorder = false, int typeId = -1)
    {
        var cell = cellPool.Require();
        cell.Init(cellShapeType, pos, rot, scale, isBorder, typeId);
        return cell;
    }

    public IReadOnlyList<Vector2> GetSharedLocalVertices(CellShapeType shapeType)
    {
        return sharedLocalVertices[(int)shapeType];
    }

    public float GetSharedInradiusRatio(CellShapeType shapeType)
    {
        return sharedInradiusRatios[(int)shapeType];
    }

    public float GetSharedAreaRatio(CellShapeType shapeType)
    {
        return sharedAreaRatios[(int)shapeType];
    }

    public Material GetSharedPolygonMaterial(CellShapeType shapeType)
    {
        return sharedPolygonMaterials[(int)shapeType];
    }

    public Material GetSharedPolygonBorderMaterial(CellShapeType shapeType)
    {
        return sharedPolygonBorderMaterials[(int)shapeType];
    }

    private void BuildSharedLocalVertices()
    {
        sharedLocalVertices.Clear();

        for (int n = 3; n <= 12; n++)
        {
            sharedLocalVertices.Add(BuildRegularPolygonVertices(n, 1f));
        }
    }

    private void BuildSharedInradiusRatios()
    {
        sharedInradiusRatios.Clear();

        float baseInradius = BuildRegularPolygonInradius(3, 1f);

        for (int n = 3; n <= 12; n++)
        {
            float inradius = BuildRegularPolygonInradius(n, 1f);
            sharedInradiusRatios.Add(inradius / baseInradius);
        }
    }

    private void BuildSharedAreaRatios()
    {
        sharedAreaRatios.Clear();

        float baseArea = BuildRegularPolygonArea(3, 1f);

        for (int n = 3; n <= 12; n++)
        {
            float area = BuildRegularPolygonArea(n, 1f);
            sharedAreaRatios.Add(area / baseArea);
        }
    }

    private void BuildSharedPolygonMaterials()
    {
        sharedPolygonMaterials.Clear();
        sharedPolygonBorderMaterials.Clear();

        for (int i = 0; i < ShapeCount; i++)
        {
            Material mat = new Material(mainDataSO.polygonBaseMaterial)
            {
                name = $"{mainDataSO.polygonBaseMaterial.name}_Shape_{i + 3}"
            };

            mat.SetTexture(SdfTexId, mainDataSO.polygonSDFTextures[i]);
            mat.SetFloat(BevelWidthId, mainDataSO.bevelSize / sharedInradiusRatios[i]);
            sharedPolygonMaterials.Add(mat);

            Material borderMat = new Material(mainDataSO.polygonBorderMaterial)
            {
                name = $"{mainDataSO.polygonBorderMaterial.name}_Shape_{i + 3}"
            };

            borderMat.SetTexture(SdfTexId, mainDataSO.polygonSDFTextures[i]);
            borderMat.SetFloat(BevelWidthId, mainDataSO.bevelSize / sharedInradiusRatios[i]);
            sharedPolygonBorderMaterials.Add(borderMat);
        }
    }

    private static Vector2[] BuildRegularPolygonVertices(int n, float size)
    {
        var vertices = new Vector2[n];
        float step = Mathf.PI * 2f / n;

        float radius = size / (2f * Mathf.Sin(Mathf.PI / n));
        float startAngle = -Mathf.PI * 0.5f - step * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float angle = startAngle + step * i;
            vertices[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return vertices;
    }

    private static float BuildRegularPolygonInradius(int n, float size)
    {
        return size / (2f * Mathf.Tan(Mathf.PI / n));
    }

    private static float BuildRegularPolygonArea(int n, float size)
    {
        return n * size * size / (4f * Mathf.Tan(Mathf.PI / n));
    }
}