using System.IO;
using UnityEngine;

[CreateAssetMenu(fileName = "GradientTextureGenerator", menuName = "Minesweeper/GradientTextureGenerator", order = 0)]
public class GradientTextureGenerator : ScriptableObject
{
    private enum GradientShape
    {
        Linear,
        Radial
    }

    private enum GradientColorMode
    {
        Grayscale,
        Hue,
        LightnessFromBaseColor
    }

    [Header("输出")]
    [SerializeField] private string outputFolder = "Assets/Texture/Gradients";
    [SerializeField] private string fileName = "Gradient.png";
    [SerializeField] private int textureSize = 512;

    [Header("GPU")]
    [SerializeField] private ComputeShader gradientComputeShader;

    [Header("形状")]
    [SerializeField] private GradientShape shape = GradientShape.Linear;
    [SerializeField] private Vector2 linearDirection = Vector2.right;
    [SerializeField] private Vector2 radialCenter01 = new Vector2(0.5f, 0.5f);

    [Header("颜色模式")]
    [SerializeField] private GradientColorMode colorMode = GradientColorMode.Hue;

    [Header("通用端点颜色（用于灰度/色相模式）")]
    [SerializeField] private Color startColor = Color.red;
    [SerializeField] private Color endColor = Color.blue;

    [Header("色相模式参数")]
    [Range(0f, 1f)][SerializeField] private float hueModeLightness = 0.72f;
    [Range(0f, 1f)][SerializeField] private float hueModeChroma = 0.16f;
    [SerializeField] private bool hueShortestPath = true;

    [Header("单色亮度模式参数")]
    [SerializeField] private Color baseColor = Color.cyan;
    [Range(0f, 1f)][SerializeField] private float lightnessStart = 0.2f;
    [Range(0f, 1f)][SerializeField] private float lightnessEnd = 0.85f;

#if UNITY_EDITOR
    public void GenerateGradientPng()
    {
        int size = Mathf.Max(2, textureSize);
        string safeFileName = SanitizeFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "Gradient.png";
        }

        if (!safeFileName.EndsWith(".png"))
        {
            safeFileName += ".png";
        }

        string normalizedFolder = NormalizePath(outputFolder);
        string fullFolder = ResolveFullFolderPath(normalizedFolder);
        Directory.CreateDirectory(fullFolder);

        Texture2D tex = BuildGradientTexture(size, true);
        if (tex == null)
        {
            Debug.LogError("GPU 渐变生成失败。");
            return;
        }

        string fullPath = Path.Combine(fullFolder, safeFileName);
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        DestroyImmediate(tex);

        string assetPath = TryConvertToAssetPath(fullPath);
        if (!string.IsNullOrEmpty(assetPath))
        {
            UnityEditor.AssetDatabase.ImportAsset(assetPath, UnityEditor.ImportAssetOptions.ForceUpdate);
            //UnityEditor.Selection.activeObject = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        UnityEditor.AssetDatabase.Refresh();
        Debug.Log($"已生成渐变贴图: {fullPath}");
    }
#endif

    public Texture2D BuildGradientTexture(int requestedSize)
    {
        return BuildGradientTexture(requestedSize, true);
    }

    public Texture2D BuildGradientTexture(int requestedSize, bool _)
    {
        int size = Mathf.Max(2, requestedSize);

        if (TryBuildGradientTextureGpu(size, out Texture2D gpuTex))
        {
            return gpuTex;
        }

        return BuildErrorTexture(size);
    }

    private bool TryBuildGradientTextureGpu(int size, out Texture2D texture)
    {
        texture = null;

        if (gradientComputeShader == null)
        {
            Debug.LogError("`gradientComputeShader` 未绑定。");
            return false;
        }

        if (!SystemInfo.supportsComputeShaders)
        {
            Debug.LogError("当前平台不支持 Compute Shader。");
            return false;
        }

        int kernel;
        try
        {
            kernel = gradientComputeShader.FindKernel("CSMain");
        }
        catch
        {
            Debug.LogError("Compute Shader 中未找到 kernel: CSMain。");
            return false;
        }

        ColorGenerator.SrgbToOklch(startColor, out float grayStartL, out _, out float hueStart);
        ColorGenerator.SrgbToOklch(endColor, out float grayEndL, out _, out float hueEnd);
        ColorGenerator.SrgbToOklch(baseColor, out _, out float baseChroma, out float baseHue);

        float alphaStart = Mathf.Clamp01(startColor.a);
        float alphaEnd = Mathf.Clamp01(endColor.a);
        float alphaBase = Mathf.Clamp01(baseColor.a);

        RenderTexture rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.enableRandomWrite = true;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();

        gradientComputeShader.SetTexture(kernel, "Result", rt);
        gradientComputeShader.SetInt("_Size", size);
        gradientComputeShader.SetInt("_Shape", (int)shape);
        gradientComputeShader.SetInt("_ColorMode", (int)colorMode);

        gradientComputeShader.SetVector("_LinearDirection", linearDirection);
        gradientComputeShader.SetVector("_RadialCenter01", radialCenter01);

        gradientComputeShader.SetFloat("_HueModeLightness", hueModeLightness);
        gradientComputeShader.SetFloat("_HueModeChroma", hueModeChroma);
        gradientComputeShader.SetInt("_HueShortestPath", hueShortestPath ? 1 : 0);

        gradientComputeShader.SetFloat("_LightnessStart", lightnessStart);
        gradientComputeShader.SetFloat("_LightnessEnd", lightnessEnd);

        gradientComputeShader.SetFloat("_GrayStartL", grayStartL);
        gradientComputeShader.SetFloat("_GrayEndL", grayEndL);
        gradientComputeShader.SetFloat("_HueStart", hueStart);
        gradientComputeShader.SetFloat("_HueEnd", hueEnd);
        gradientComputeShader.SetFloat("_BaseHue", baseHue);
        gradientComputeShader.SetFloat("_BaseChroma", baseChroma);

        gradientComputeShader.SetFloat("_AlphaStart", alphaStart);
        gradientComputeShader.SetFloat("_AlphaEnd", alphaEnd);
        gradientComputeShader.SetFloat("_AlphaBase", alphaBase);

        int groups = Mathf.CeilToInt(size / 8f);
        gradientComputeShader.Dispatch(kernel, groups, groups, 1);

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0f, 0f, size, size), 0, 0, false);
        tex.Apply(false, false);
        RenderTexture.active = prev;

        rt.Release();
        DestroyImmediate(rt);

        texture = tex;
        return true;
    }

    private static Texture2D BuildErrorTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color c0 = new Color(1f, 0f, 1f, 1f);
        Color c1 = new Color(0f, 0f, 0f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool checker = ((x >> 4) + (y >> 4)) % 2 == 0;
                tex.SetPixel(x, y, checker ? c0 : c1);
            }
        }

        tex.Apply(false, false);
        return tex;
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        string result = name;
        for (int i = 0; i < invalid.Length; i++)
        {
            result = result.Replace(invalid[i], '_');
        }

        return result.Trim();
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "Assets" : path.Replace("\\", "/").TrimEnd('/');
    }

    private static string ResolveFullFolderPath(string folderPath)
    {
        if (Path.IsPathRooted(folderPath))
        {
            return folderPath;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
        if (folderPath.StartsWith("Assets"))
        {
            string relative = folderPath.Length > "Assets".Length ? folderPath.Substring("Assets".Length).TrimStart('/') : string.Empty;
            return Path.Combine(Application.dataPath, relative).Replace("\\", "/");
        }

        return Path.Combine(projectRoot, folderPath).Replace("\\", "/");
    }

    private static string TryConvertToAssetPath(string fullPath)
    {
        string dataPath = Application.dataPath.Replace("\\", "/");
        string normalized = fullPath.Replace("\\", "/");

        if (!normalized.StartsWith(dataPath))
        {
            return null;
        }

        return "Assets" + normalized.Substring(dataPath.Length);
    }
}