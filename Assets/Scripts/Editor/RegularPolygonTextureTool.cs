using System.IO;
using UnityEditor;
using UnityEngine;

public static class RegularPolygonTextureTool
{
    private const int MinSides = 3;
    private const int MaxSides = 12;
    private const int TextureSize = 256; // 2^n
    private const int PaddingPixels = 3;
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
        float radius = center - PaddingPixels - 0.5f; // keep pixels fully inside

        Vector2[] vertices = BuildVertices(sides, radius);

        Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[TextureSize * TextureSize];
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float px = (x + 0.5f) - center;
                float py = (y + 0.5f) - center;

                bool inside = IsPointInPolygon(new Vector2(px, py), vertices);
                pixels[y * TextureSize + x] = inside ? white : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false); // 改为可读，供 EncodeToPNG 使用

        string fileName = $"RegularPolygon_{sides}.png";
        string assetPath = $"{OutputDir}/{fileName}";
        byte[] pngBytes = tex.EncodeToPNG();
        File.WriteAllBytes(assetPath, pngBytes);

        Object.DestroyImmediate(tex);

        ConfigureImporter(assetPath, sides, radius);
    }

    private static Vector2[] BuildVertices(int sides, float radius)
    {
        Vector2[] vertices = new Vector2[sides];
        float step = Mathf.PI * 2f / sides;

        // 保证“正下方是边”：让 -90° 方向落在一条边的中点方向
        float startAngle = -Mathf.PI * 0.5f - step * 0.5f;

        for (int i = 0; i < sides; i++)
        {
            float angle = startAngle + step * i;
            vertices[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return vertices;
    }

    private static void ConfigureImporter(string assetPath, int sides, float radius)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        // 像素边长 -> 世界边长=1
        float sidePixels = 2f * radius * Mathf.Sin(Mathf.PI / sides);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = sidePixels;
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