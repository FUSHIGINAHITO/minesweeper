using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

public static class NumberTextureGenerator
{
    private const int MinNumber = 1;
    private const int MaxNumber = 20;
    private const int TextureSize = 256;
    private const string OutputRoot = "Assets/Texture";
    private const string OutputFolder = OutputRoot + "/Numbers";
    private const int TempLayer = 31; // 使用高位临时层，克隆对象会被设置到此层以便相机剔除

    [MenuItem("Tools/Generate Number Textures (1-20) From Selected TMP")]
    public static void GenerateFromSelected()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogError("请先在层级视图中选中包含 TMP_Text 的 GameObject。");
            return;
        }

        TMP_Text sourceTmp = Selection.activeGameObject.GetComponentInChildren<TMP_Text>();
        if (sourceTmp == null)
        {
            Debug.LogError("选中的对象或其子对象未找到 TMP_Text 组件（TextMeshPro）。");
            return;
        }

        EnsureOutputFolder();

        // 克隆选中对象，避免修改原对象
        GameObject clone = Object.Instantiate(Selection.activeGameObject);
        clone.name = "TMP_Clone_ForCapture";
        clone.hideFlags = HideFlags.DontSave;

        // 将克隆及子对象都置为临时层
        SetLayerRecursively(clone, TempLayer);

        // 寻找克隆内的 TMP_Text 实例（用于逐数字设置）
        TMP_Text tmp = clone.GetComponentInChildren<TMP_Text>();
        if (tmp == null)
        {
            Object.DestroyImmediate(clone);
            Debug.LogError("克隆对象中未找到 TMP_Text（应与原对象相同结构）。");
            return;
        }

        // 强制白色、不透明（输出为白字）
        Color originalColor = tmp.color;
        tmp.color = Color.white;

        // 计算克隆对象的包围盒（基于 renderer）
        Renderer[] rends = clone.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0)
        {
            tmp.color = originalColor;
            Object.DestroyImmediate(clone);
            Debug.LogError("未找到任何 Renderer，无法计算包围范围。请确保使用的是 World-space TextMeshPro（非 UGUI）。");
            return;
        }

        Bounds bounds = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
        {
            bounds.Encapsulate(rends[i].bounds);
        }

        // 创建临时相机
        GameObject camGO = new GameObject("TMP_CaptureCam");
        camGO.hideFlags = HideFlags.DontSave;
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 透明背景
        cam.orthographic = true;
        cam.cullingMask = 1 << TempLayer;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        // 将相机放在包围盒正对面的方向（使用克隆的 forward）
        Vector3 center = bounds.center;
        Vector3 forward = clone.transform.forward;
        if (forward == Vector3.zero)
            forward = Vector3.forward;
        cam.transform.position = center - forward.normalized * 10f;
        cam.transform.rotation = Quaternion.LookRotation(forward, clone.transform.up);

        // 计算 orthographicSize（留一点边距）
        float halfHeight = bounds.extents.y;
        float halfWidth = bounds.extents.x;
        float margin = 0.05f; // 5% margin
        float orthoSize = Mathf.Max(halfHeight, halfWidth) * (1f + margin);
        if (orthoSize <= 0f)
            orthoSize = 0.5f;
        cam.orthographicSize = orthoSize;

        // 创建 RenderTexture
        RenderTexture rt = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        cam.targetTexture = rt;

        // 遍历数字并拍摄
        for (int n = MinNumber; n <= MaxNumber; n++)
        {
            tmp.text = n.ToString();
            tmp.ForceMeshUpdate(); // 立即更新

            // 渲染并读回像素
            cam.Render();

            RenderTexture current = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, TextureSize, TextureSize), 0, 0);
            tex.Apply();

            // 保存 PNG
            byte[] png = tex.EncodeToPNG();
            string fileName = $"Number_{n}.png";
            string assetPath = $"{OutputFolder}/{fileName}";
            File.WriteAllBytes(assetPath, png);

            Object.DestroyImmediate(tex);
            RenderTexture.active = current;

            // 导入 asset
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            // 启用 Mipmaps
            EnableMipmapsForAsset(assetPath);

            Debug.Log($"Saved {assetPath}");
        }

        // 恢复并清理
        tmp.color = originalColor;
        cam.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(clone);

        AssetDatabase.Refresh();
        Debug.Log($"Number textures generated in {OutputFolder}");
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder(OutputRoot))
        {
            AssetDatabase.CreateFolder("Assets", "Texture");
        }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            string child = OutputFolder.Substring(OutputRoot.Length + 1);
            AssetDatabase.CreateFolder(OutputRoot, child);
        }
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
        {
            SetLayerRecursively(t.gameObject, layer);
        }
    }

    private static void EnableMipmapsForAsset(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"无法获取 TextureImporter: {assetPath}");
            return;
        }

        // 开启 Mipmaps
        importer.mipmapEnabled = true;

        // 保持其它常用设置以便后续使用（可按需调整）
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;

        // 若这是透明 PNG，保持 alpha 源
        importer.alphaSource = TextureImporterAlphaSource.FromInput;

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }
}