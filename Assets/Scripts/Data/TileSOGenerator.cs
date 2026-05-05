using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TileSOGenerator
{
    private const string MenuPath = "Tools/Minesweeper/Generate TileSOs From Selected MainDataSO";
    private const int MinSides = 3;

    private const string MaterialRootDir = "Assets/Shader&Material";
    private const string PolygonMaterialDir = MaterialRootDir + "/Polygon";
    private const string PolygonBorderMaterialDir = MaterialRootDir + "/PolygonBorder";

    private static readonly int SdfTexId = Shader.PropertyToID("_SDFTex");
    private static readonly int BevelWidthId = Shader.PropertyToID("_BevelWidth");

    [MenuItem(MenuPath, true)]
    private static bool ValidateGenerateTileSOs()
    {
        return Selection.activeObject is MainDataSO;
    }

    [MenuItem(MenuPath)]
    private static void GenerateTileSOs()
    {
        if (Selection.activeObject is not MainDataSO mainDataSO)
        {
            EditorUtility.DisplayDialog("Generate TileSOs", "请先在 Project 里选中一个 MainDataSO。", "OK");
            return;
        }

        if (mainDataSO.polygonBaseMaterial == null || mainDataSO.polygonBorderMaterial == null)
        {
            EditorUtility.DisplayDialog(
                "Generate TileSOs",
                "MainDataSO 上的 polygonBaseMaterial 或 polygonBorderMaterial 为空。",
                "OK");
            return;
        }

        string mainDataPath = AssetDatabase.GetAssetPath(mainDataSO);
        string mainDataDir = Path.GetDirectoryName(mainDataPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(mainDataDir))
        {
            Debug.LogError("无法解析 MainDataSO 路径。");
            return;
        }

        string tileRootDir = $"{mainDataDir}/Tiles";
        EnsureFolder(tileRootDir);

        EnsureFolder(MaterialRootDir);
        EnsureFolder(PolygonMaterialDir);
        EnsureFolder(PolygonBorderMaterialDir);

        int shapeCount = System.Enum.GetValues(typeof(CellShapeType)).Length;
        float baseInradius = BuildRegularPolygonInradius(MinSides, 1f);
        float baseArea = BuildRegularPolygonArea(MinSides, 1f);

        var generatedTiles = new List<TileSO>(shapeCount);

        for (int i = 0; i < shapeCount; i++)
        {
            CellShapeType shapeType = (CellShapeType)i;
            int sides = i + MinSides;

            string tileAssetPath = $"{tileRootDir}/{shapeType}.asset";
            TileSO tile = AssetDatabase.LoadAssetAtPath<TileSO>(tileAssetPath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<TileSO>();
                AssetDatabase.CreateAsset(tile, tileAssetPath);
            }

            tile.shapeType = shapeType;

            // 默认参数（可在单个 Tile 上继续手调）
            if (tile.bevelSize <= 0f)
            {
                tile.bevelSize = mainDataSO.bevelSize;
            }

            float inradius = BuildRegularPolygonInradius(sides, 1f);
            float area = BuildRegularPolygonArea(sides, 1f);

            tile.inradiusRatio = inradius / baseInradius;
            tile.areaRatio = area / baseArea;
            tile.localVertices = BuildRegularPolygonVertices(sides, 1f);

            // 生成普通材质
            string matPath = $"{PolygonMaterialDir}/{shapeType}.mat";
            Material polygonMat = CreateOrUpdateMaterialAsset(
                matPath,
                mainDataSO.polygonBaseMaterial,
                tile,
                sides);

            // 生成边框材质
            string borderMatPath = $"{PolygonBorderMaterialDir}/{shapeType}.mat";
            Material polygonBorderMat = CreateOrUpdateMaterialAsset(
                borderMatPath,
                mainDataSO.polygonBorderMaterial,
                tile,
                sides);

            tile.polygonMaterialOverride = polygonMat;
            tile.polygonBorderMaterialOverride = polygonBorderMat;

            EditorUtility.SetDirty(tile);
            generatedTiles.Add(tile);
        }

        // 将 tiles 列表回填到 MainDataSO（不依赖 OnValidate 也能立即生效）
        AssignTilesToMainData(mainDataSO, generatedTiles);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Generate TileSOs",
            $"已生成/更新 {shapeCount} 个 TileSO。\n材质输出：\n{PolygonMaterialDir}\n{PolygonBorderMaterialDir}",
            "OK");
    }

    private static Material CreateOrUpdateMaterialAsset(string assetPath, Material template, TileSO tile, int sides)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (mat == null)
        {
            mat = new Material(template);
            mat.name = $"{template.name}_Shape_{sides}";
            AssetDatabase.CreateAsset(mat, assetPath);
        }
        else
        {
            if (mat.shader != template.shader)
            {
                mat.shader = template.shader;
            }
        }

        if (tile.polygonSDFTexture != null)
        {
            mat.SetTexture(SdfTexId, tile.polygonSDFTexture);
        }

        float bevel = tile.bevelSize;
        float ratio = Mathf.Max(1e-6f, tile.inradiusRatio);
        mat.SetFloat(BevelWidthId, bevel / ratio);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void AssignTilesToMainData(MainDataSO mainDataSO, List<TileSO> tiles)
    {
        SerializedObject so = new SerializedObject(mainDataSO);
        SerializedProperty tilesProp = so.FindProperty("tiles");
        if (tilesProp == null || !tilesProp.isArray)
        {
            Debug.LogWarning("MainDataSO 未找到私有字段 tiles，跳过自动回填。");
            return;
        }

        tiles.Sort((a, b) => a.shapeType.CompareTo(b.shapeType));

        tilesProp.arraySize = tiles.Count;
        for (int i = 0; i < tiles.Count; i++)
        {
            SerializedProperty item = tilesProp.GetArrayElementAtIndex(i);
            item.objectReferenceValue = tiles[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mainDataSO);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
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