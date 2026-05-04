using System.IO;
using UnityEditor;
using UnityEngine;

public static class RegularPolygonTextureTool
{
    private const int MinSides = 3;
    private const int MaxSides = 12;
    private const int TextureSize = 256; // 2^n
    private const int PaddingPixels = 3;
    private const float InradiusShrink = 0.02f; // SpriteRenderer 中中心到边距离减少量（世界单位）
    private const float InradiusShrinkSDF = 0.00f; // SpriteRenderer 中中心到边距离减少量（世界单位）
    private const int Supersample = 8; // 8x8 SSAA
    private const int SdfSpreadPixels = 64; // 建议 48~64，避免 8bit 量化导致法线坑洼

    private const string OutputRootDir = "Assets/Texture";
    private const string PolygonNoShrinkDir = OutputRootDir + "/Polygon";
    private const string PolygonShrinkDir = OutputRootDir + "/Polygon_Shrink";
    private const string PolygonSdfDir = OutputRootDir + "/PolygonSDF";

    [MenuItem("Tools/Generate Regular Polygon PNGs (3-12)")]
    public static void GenerateRegularPolygonPngs()
    {
        EnsureOutputFolder(PolygonNoShrinkDir);
        EnsureOutputFolder(PolygonShrinkDir);

        for (int sides = MinSides; sides <= MaxSides; sides++)
        {
            // 不带 InradiusShrink
            GenerateOne(sides, 0f, PolygonNoShrinkDir);

            // 带 InradiusShrink
            GenerateOne(sides, InradiusShrink, PolygonShrinkDir);
        }

        AssetDatabase.Refresh();
        Debug.Log($"Regular polygon PNGs generated in {PolygonNoShrinkDir} and {PolygonShrinkDir}.");
    }

    [MenuItem("Tools/Generate Regular Polygon SDF PNGs (3-12)")]
    public static void GenerateRegularPolygonSdfPngs()
    {
        EnsureOutputFolder(PolygonSdfDir);

        for (int sides = MinSides; sides <= MaxSides; sides++)
        {
            GenerateOneSdf(sides);
        }

        AssetDatabase.Refresh();
        Debug.Log($"Regular polygon SDF PNGs generated in {PolygonSdfDir}.");
    }

    private static void EnsureOutputFolder(string targetDir)
    {
        if (!AssetDatabase.IsValidFolder(OutputRootDir))
        {
            AssetDatabase.CreateFolder("Assets", "Texture");
        }

        if (!AssetDatabase.IsValidFolder(targetDir))
        {
            string childFolderName = targetDir.Substring(OutputRootDir.Length + 1);
            AssetDatabase.CreateFolder(OutputRootDir, childFolderName);
        }
    }

    private static void GenerateOne(int sides, float inradiusShrink, string targetDir)
    {
        float center = TextureSize * 0.5f;
        float fitRadius = center - PaddingPixels - 0.5f; // 未缩小时可铺满的外接圆半径（像素）

        float unitInradius = 1f / (2f * Mathf.Tan(Mathf.PI / sides));
        float targetUnitInradius = Mathf.Max(1e-4f, unitInradius - inradiusShrink);
        float shrinkFactor = targetUnitInradius / unitInradius;

        float drawRadius = fitRadius * shrinkFactor;
        Vector2[] vertices = BuildVertices(sides, drawRadius);

        Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float alpha01 = CalculateCoverage(x, y, center, vertices);
                byte a = (byte)Mathf.RoundToInt(alpha01 * 255f);
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        string fileName = $"RegularPolygon_{sides}.png";
        string assetPath = $"{targetDir}/{fileName}";
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());

        Object.DestroyImmediate(tex);

        float baseSidePixels = 2f * fitRadius * Mathf.Sin(Mathf.PI / sides);
        ConfigureImporter(assetPath, baseSidePixels, true);
    }

    private static void GenerateOneSdf(int sides)
    {
        float center = TextureSize * 0.5f;
        float fitRadius = center - PaddingPixels - 0.5f;

        float unitInradius = 1f / (2f * Mathf.Tan(Mathf.PI / sides));
        float targetUnitInradius = Mathf.Max(1e-4f, unitInradius - InradiusShrinkSDF);
        float shrinkFactor = targetUnitInradius / unitInradius;

        float drawRadius = fitRadius * shrinkFactor;
        Vector2[] vertices = BuildVertices(sides, drawRadius);

        Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - center, y + 0.5f - center);
                float signedDistance = SignedDistanceToPolygon(p, vertices); // 内部为正，外部为负
                float sdf01 = Mathf.Clamp01(0.5f + signedDistance / (2f * SdfSpreadPixels));

                // alpha 固定 255，SDF 数据放在 RGB
                byte v = (byte)Mathf.RoundToInt(sdf01 * 255f);
                pixels[y * TextureSize + x] = new Color32(v, v, v, 255);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        string fileName = $"RegularPolygon_{sides}_SDF.png";
        string assetPath = $"{PolygonSdfDir}/{fileName}";
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());

        Object.DestroyImmediate(tex);

        float baseSidePixels = 2f * fitRadius * Mathf.Sin(Mathf.PI / sides);
        ConfigureImporter(assetPath, baseSidePixels, false);
    }

    private static float CalculateCoverage(int x, int y, float center, Vector2[] vertices)
    {
        int insideCount = 0;
        int total = Supersample * Supersample;
        float inv = 1f / Supersample;

        for (int sy = 0; sy < Supersample; sy++)
        {
            for (int sx = 0; sx < Supersample; sx++)
            {
                float subX = x + (sx + 0.5f) * inv;
                float subY = y + (sy + 0.5f) * inv;

                float px = subX - center;
                float py = subY - center;

                if (IsPointInPolygon(new Vector2(px, py), vertices))
                {
                    insideCount++;
                }
            }
        }

        return (float)insideCount / total;
    }

    private static Vector2[] BuildVertices(int sides, float radius)
    {
        Vector2[] vertices = new Vector2[sides];
        float step = Mathf.PI * 2f / sides;

        // 正下方是边
        float startAngle = -Mathf.PI * 0.5f - step * 0.5f;

        for (int i = 0; i < sides; i++)
        {
            float angle = startAngle + step * i;
            vertices[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return vertices;
    }

    private static void ConfigureImporter(string assetPath, float spritePixelsPerUnit, bool alphaIsTransparency)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = spritePixelsPerUnit;
        importer.spritePivot = new Vector2(0.5f, 0.5f);

        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = alphaIsTransparency;

        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;

        if (!alphaIsTransparency)
        {
            importer.sRGBTexture = false;
        }

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static float SignedDistanceToPolygon(Vector2 p, Vector2[] polygon)
    {
        float minDistance = float.MaxValue;
        int n = polygon.Length;

        for (int i = 0; i < n; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % n];
            float d = DistancePointToSegment(p, a, b);
            if (d < minDistance)
            {
                minDistance = d;
            }
        }

        bool inside = IsPointInPolygon(p, polygon);
        return inside ? minDistance : -minDistance;
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denom = Vector2.Dot(ab, ab);
        if (denom <= 1e-12f)
        {
            return Vector2.Distance(p, a);
        }

        float t = Vector2.Dot(p - a, ab) / denom;
        t = Mathf.Clamp01(t);

        Vector2 q = a + ab * t;
        return Vector2.Distance(p, q);
    }

    private static bool IsPointInPolygon(Vector2 p, Vector2[] polygon)
    {
        bool inside = false;
        int n = polygon.Length;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];

            bool intersect = ((a.y > p.y) != (b.y > p.y))
                && (p.x < (b.x - a.x) * (p.y - a.y) / ((b.y - a.y) + 1e-12f) + a.x);

            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}