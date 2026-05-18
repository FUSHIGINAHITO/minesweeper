using System.IO;
using UnityEditor;
using UnityEngine;

public static class MineFlagTextureGenerator
{
    private const int TextureSize = 256;
    private const int Supersample = 4; // 每像素采样数 (Supersample x Supersample)
    private const string OutputRoot = "Assets/Texture";
    private const string OutputFolder = OutputRoot + "/Minefield";

    [MenuItem("Tools/Generate Mines & Flag Textures")]
    public static void Generate()
    {
        EnsureOutputFolder();

        // 生成地雷
        string minePath = $"{OutputFolder}/Mine.png";
        Texture2D mineTex = GenerateMine(TextureSize);
        File.WriteAllBytes(minePath, mineTex.EncodeToPNG());
        Object.DestroyImmediate(mineTex);
        AssetDatabase.ImportAsset(minePath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(minePath, true);

        // 生成旗帜
        string flagPath = $"{OutputFolder}/Flag.png";
        Texture2D flagTex = GenerateFlag(TextureSize);
        File.WriteAllBytes(flagPath, flagTex.EncodeToPNG());
        Object.DestroyImmediate(flagTex);
        AssetDatabase.ImportAsset(flagPath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(flagPath, true);

        AssetDatabase.Refresh();
        Debug.Log($"Generated Mine and Flag textures in {OutputFolder}");
    }

    private static Texture2D GenerateMine(int size)
    {
        Color32[] pixels = new Color32[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float radius = size * 0.38f; // 球体半径
        float rimThickness = radius * 0.10f;
        Color sphereColor = new Color(0.06f, 0.06f, 0.06f, 1f); // 深黑灰
        Color rimColor = new Color(0.18f, 0.18f, 0.18f, 1f); // 边缘浅灰
        Color shineColor = new Color(1f, 1f, 1f, 0.85f);
        Color shadowColor = new Color(0f, 0f, 0f, 0.6f);

        // 小金属棘（spikes）参数
        int spikeCount = 8;
        float spikeLength = radius * 0.45f;
        float spikeWidth = radius * 0.08f;
        Color spikeColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        // 每像素 supersample
        int ss = Supersample;
        float invSS = 1f / ss;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 累加 RGBA
                float ar = 0f, ag = 0f, ab = 0f, aa = 0f;

                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        // sample position in pixel space (subpixel)
                        float sampleX = x + (sx + 0.5f) * invSS;
                        float sampleY = y + (sy + 0.5f) * invSS;

                        // local coords relative to center
                        float lx = sampleX - cx;
                        float ly = sampleY - cy;
                        float dist = Mathf.Sqrt(lx * lx + ly * ly);

                        // start with transparent
                        Color src = new Color(0, 0, 0, 0);

                        // spikes: thin rectangles radiating
                        bool inSpike = false;
                        for (int i = 0; i < spikeCount; i++)
                        {
                            float ang = i * Mathf.PI * 2f / spikeCount;
                            // rotate point by -ang
                            float rx = lx * Mathf.Cos(-ang) - ly * Mathf.Sin(-ang);
                            float ry = lx * Mathf.Sin(-ang) + ly * Mathf.Cos(-ang);
                            // spike occupies region: rx in [radius - spikeLength, radius + small], ry in [-spikeWidth, spikeWidth]
                            if (rx > radius - spikeLength && rx < radius + spikeWidth * 2f && Mathf.Abs(ry) < spikeWidth)
                            {
                                inSpike = true;
                                break;
                            }
                        }
                        if (inSpike)
                        {
                            src = spikeColor;
                        }

                        // sphere rim and main body
                        if (dist <= radius)
                        {
                            // rim blend factor
                            float t = Mathf.InverseLerp(radius - rimThickness, radius, dist);
                            Color body = Color.Lerp(rimColor, sphereColor, t); // 更靠内侧更深
                            // blend atop spike
                            src = AlphaBlend(body, src);
                        }

                        // highlight (shine) - an elliptical highlight on upper-left (保留)
                        float hx = lx + radius * 0.35f;
                        float hy = ly - radius * 0.45f;
                        float hxR = radius * 0.42f;
                        float hyR = radius * 0.28f;
                        if ((hx * hx) / (hxR * hxR) + (hy * hy) / (hyR * hyR) <= 1f)
                        {
                            // stronger near center of highlight
                            float hv = 1f - Mathf.Sqrt((hx * hx) / (hxR * hxR) + (hy * hy) / (hyR * hyR));
                            Color shine = shineColor;
                            shine.a *= hv * 0.85f;
                            src = AlphaBlend(shine, src);
                        }

                        // bottom shadow (subtle drop-shadow inside)
                        if (dist <= radius)
                        {
                            float shadowFactor = Mathf.InverseLerp(radius, radius - rimThickness * 2f, dist);
                            Color shadow = shadowColor;
                            shadow.a *= (1f - shadowFactor) * 0.25f;
                            src = AlphaBlend(shadow, src);
                        }

                        // convert to premultiplied sampling accumulation
                        ar += src.r * src.a;
                        ag += src.g * src.a;
                        ab += src.b * src.a;
                        aa += src.a;
                    }
                }

                // average samples
                float samples = ss * ss;
                if (aa > 0f)
                {
                    // un-premultiply
                    float r = ar / aa;
                    float g = ag / aa;
                    float b = ab / aa;
                    float a = aa / samples;
                    // clamp and write
                    byte br = (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255);
                    byte bg = (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255);
                    byte bb = (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255);
                    byte ba = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(br, bg, bb, ba);
                }
                else
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static Texture2D GenerateFlag(int size)
    {
        Color32[] pixels = new Color32[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;

        // 更卡通的旗杆与旗帜参数，且把旗帜重心尽量靠近图片中心
        float poleHeight = size * 0.72f;
        float poleWidth = Mathf.Max(3f, size * 0.04f); // 棒更粗，卡通感
        float poleX = cx - size * 0.10f; // 更靠近中心（而不是太左）
        float poleTop = cy + poleHeight * 0.5f;
        float poleBottom = cy - poleHeight * 0.5f;

        // 旗帜基点：左侧控制点靠近杆
        float f0x = poleX + poleWidth * 0.6f;
        float f0y = poleTop - size * 0.06f;
        Vector2 f0 = new Vector2(f0x, f0y);
        // 使旗帜重心水平居中：计算 tipX 使三角形质心 x 接近 cx
        float tipX = 3f * cx - 2f * f0x;
        // 限制 tipX，避免超出画布
        tipX = Mathf.Clamp(tipX, f0x + size * 0.12f, size - size * 0.08f);
        Vector2 f1 = new Vector2(tipX, cy); // 旗尖
        Vector2 f2 = new Vector2(f0x, cy + size * 0.02f); // 稍微下移一点以制造卡通感

        // colors（更鲜明的卡通色）
        Color poleColor = new Color(0.36f, 0.24f, 0.12f, 1f); // 木杆深棕
        Color flagColor = new Color(0.95f, 0.12f, 0.20f, 1f); // 更鲜红
        Color flagHighlight = new Color(1f, 1f, 1f, 0.28f);
        Color outline = new Color(0f, 0f, 0f, 1f);

        int ss = Supersample;
        float invSS = 1f / ss;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ar = 0f, ag = 0f, ab = 0f, aa = 0f;

                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float sampleX = x + (sx + 0.5f) * invSS;
                        float sampleY = y + (sy + 0.5f) * invSS;

                        Color src = new Color(0, 0, 0, 0);

                        // pole as rounded rectangle/vertical bar
                        if (sampleX >= poleX && sampleX <= poleX + poleWidth && sampleY <= poleTop && sampleY >= poleBottom)
                        {
                            src = poleColor;
                        }

                        // 旗帜三角形填充（没有阴影，保持扁平卡通色），但加一点从杆到尖的饱和度过渡让视觉更丰富
                        if (PointInTriangle(new Vector2(sampleX, sampleY), f0, f1, f2))
                        {
                            // 横向因子：越靠近杆颜色越纯，靠近尖端略微变暗一点以表现体积
                            float t = Mathf.InverseLerp(f0x, f1.x, sampleX);
                            t = Mathf.Clamp01(t);
                            // 轻微变暗，但不是阴影
                            Color col = Color.Lerp(flagColor, flagColor * 0.85f, t * 0.3f);
                            src = AlphaBlend(col, src);
                        }

                        // 卡通高光（保留但轻量），不要阴影
                        float hx = (sampleX - (f0.x + f1.x) * 0.5f) / (size * 0.25f);
                        float hy = (sampleY - (cy - size * 0.06f)) / (size * 0.12f);
                        float hvlen = hx * hx + hy * hy;
                        if (hvlen <= 1f)
                        {
                            float hv = 1f - Mathf.Sqrt(hvlen);
                            Color sh = flagHighlight;
                            sh.a *= hv * 0.7f;
                            src = AlphaBlend(sh, src);
                        }

                        // 保留细线描边（靠近边缘时加深）
                        float edgeDist = DistanceToTriangleEdge(new Vector2(sampleX, sampleY), f0, f1, f2);
                        if (edgeDist < 1.0f)
                        {
                            if (PointInTriangle(new Vector2(sampleX, sampleY), f0, f1, f2))
                            {
                                Color ol = outline;
                                ol.a = Mathf.Clamp01((1f - edgeDist) * 0.9f);
                                // 细描边使卡通更醒目
                                src = AlphaBlend(ol, src);
                            }
                        }

                        ar += src.r * src.a;
                        ag += src.g * src.a;
                        ab += src.b * src.a;
                        aa += src.a;
                    }
                }

                float samples = ss * ss;
                if (aa > 0f)
                {
                    float r = ar / aa;
                    float g = ag / aa;
                    float b = ab / aa;
                    float a = aa / samples;
                    byte br = (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255);
                    byte bg = (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255);
                    byte bb = (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255);
                    byte ba = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(br, bg, bb, ba);
                }
                else
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    // 简单的 alpha 混合： src over dst
    private static Color AlphaBlend(Color src, Color dst)
    {
        float outA = src.a + dst.a * (1f - src.a);
        if (outA <= 0f)
            return new Color(0, 0, 0, 0);
        float outR = (src.r * src.a + dst.r * dst.a * (1f - src.a)) / outA;
        float outG = (src.g * src.a + dst.g * dst.a * (1f - src.a)) / outA;
        float outB = (src.b * src.a + dst.b * dst.a * (1f - src.a)) / outA;
        return new Color(outR, outG, outB, outA);
    }

    // 三角形包含测试（2D）
    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float w1 = (a.x * (c.y - a.y) + (p.y - a.y) * (c.x - a.x) - p.x * (c.y - a.y)) /
                   ((b.y - a.y) * (c.x - a.x) - (b.x - a.x) * (c.y - a.y) + 1e-9f);
        float w2 = (p.y - a.y - w1 * (b.y - a.y)) / (c.y - a.y + 1e-9f);
        return w1 >= -0.001f && w2 >= -0.001f && (w1 + w2) <= 1.001f;
    }

    // 点到三角形边最短距离（用于描边效果），返回像素单位距离
    private static float DistanceToTriangleEdge(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = DistancePointToSegment(p, a, b);
        float d2 = DistancePointToSegment(p, b, c);
        float d3 = DistancePointToSegment(p, c, a);
        return Mathf.Min(d1, Mathf.Min(d2, d3));
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float ab2 = Vector2.Dot(ab, ab);
        if (ab2 == 0f)
            return Vector2.Distance(p, a);
        float t = Vector2.Dot(p - a, ab) / ab2;
        t = Mathf.Clamp01(t);
        Vector2 proj = a + ab * t;
        return Vector2.Distance(p, proj);
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

    private static void ConfigureImporter(string assetPath, bool alphaIsTransparency)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = alphaIsTransparency;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.isReadable = false;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }
}