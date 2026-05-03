using System.IO;
using UnityEditor;
using UnityEngine;

public static class RegularPolygonTextureTool
{
    private const int MinSides = 3;
    private const int MaxSides = 12;
    private const int TextureSize = 256; // 2^n
    private const int PaddingPixels = 3;
    private const float InradiusShrink = 0.05f; // SpriteRenderer 中中心到边距离减少量（世界单位）
    private const int Supersample = 8; // 4x4 SSAA
    private const string OutputDir = "Assets/Texture";

    [MenuItem("Tools/Generate Regular Polygon PNGs (Side=1, 3-12)")]
    public static void GenerateRegularPolygonPngs()
    {
        if (!AssetDatabase.IsValidFolder(OutputDir))
        {
            AssetDatabase.CreateFolder("Assets", "Texture");
        }

        for (int sides = MinSides; sides <= MaxSides; sides++)
        {
            GenerateOne(sides);
        }

        AssetDatabase.Refresh();
        Debug.Log("Regular polygon PNGs generated in Assets/Texture.");
    }

    private static void GenerateOne(int sides)
    {
        float center = TextureSize * 0.5f;
        float fitRadius = center - PaddingPixels - 0.5f; // 未缩小时可铺满的外接圆半径（像素）

        float unitInradius = 1f / (2f * Mathf.Tan(Mathf.PI / sides));
        float targetUnitInradius = Mathf.Max(1e-4f, unitInradius - InradiusShrink);
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
        string assetPath = $"{OutputDir}/{fileName}";
        byte[] pngBytes = tex.EncodeToPNG();
        File.WriteAllBytes(assetPath, pngBytes);

        Object.DestroyImmediate(tex);

        // PPU 使用未缩小前基准，保证 SpriteRenderer 下缩小量生效
        float baseSidePixels = 2f * fitRadius * Mathf.Sin(Mathf.PI / sides);
        ConfigureImporter(assetPath, baseSidePixels);
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

    private static void ConfigureImporter(string assetPath, float spritePixelsPerUnit)
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
        importer.alphaIsTransparency = true;

        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
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