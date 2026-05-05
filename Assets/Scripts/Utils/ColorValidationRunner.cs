using System.Text;
using UnityEngine;

public class ColorValidationRunner : MonoBehaviour
{
    [SerializeField] private MainDataSO data;
    [SerializeField] private int colorCount = 12;
    [SerializeField] private int randomSeed = 12345;

    [ContextMenu("Validate MainDataSO Colors")]
    private void ValidateColors()
    {
        if (data == null)
        {
            Debug.LogError("MainDataSO 未绑定。");
            return;
        }

        var oldState = Random.state;
        Random.InitState(randomSeed);
        var colors = ColorGenerator.GeneratePerceptualHueCycleColors(data, colorCount);
        Random.state = oldState;

        int outOfGamutCount = 0;
        float[] ls = new float[colors.Length];
        float[] des = new float[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            Color c = colors[i];
            if (c.r < 0f || c.r > 1f || c.g < 0f || c.g > 1f || c.b < 0f || c.b > 1f)
            {
                outOfGamutCount++;
            }

            ToOklabFromSrgb(c, out float l, out float a, out float b);
            ls[i] = l;

            int next = (i + 1) % colors.Length;
            ToOklabFromSrgb(colors[next], out float l2, out float a2, out float b2);
            float dl = l2 - l;
            float da = a2 - a;
            float db = b2 - b;
            des[i] = Mathf.Sqrt(dl * dl + da * da + db * db);
        }

        GetMeanStd(ls, out float lMean, out float lStd);
        GetMeanStd(des, out float deMean, out float deStd);

        var sb = new StringBuilder();
        sb.AppendLine($"ActiveColorSpace: {QualitySettings.activeColorSpace}");
        sb.AppendLine($"Count: {colors.Length}, Seed: {randomSeed}");
        sb.AppendLine($"OutOfGamut: {outOfGamutCount}");
        sb.AppendLine($"OKLab L mean/std: {lMean:F6} / {lStd:F6}");
        sb.AppendLine($"Adjacent ΔE(ok) mean/std: {deMean:F6} / {deStd:F6}");
        sb.AppendLine("Colors(HEX RGBA):");

        for (int i = 0; i < colors.Length; i++)
        {
            sb.AppendLine($"{i:D2}: #{ColorUtility.ToHtmlStringRGBA(colors[i])}");
        }

        Debug.Log(sb.ToString());
    }

    private static void GetMeanStd(float[] values, out float mean, out float std)
    {
        if (values == null || values.Length == 0)
        {
            mean = 0f;
            std = 0f;
            return;
        }

        float sum = 0f;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }

        mean = sum / values.Length;

        float var = 0f;
        for (int i = 0; i < values.Length; i++)
        {
            float d = values[i] - mean;
            var += d * d;
        }

        var /= values.Length;
        std = Mathf.Sqrt(var);
    }

    private static void ToOklabFromSrgb(Color srgb, out float L, out float A, out float B)
    {
        float r = SrgbToLinear(srgb.r);
        float g = SrgbToLinear(srgb.g);
        float b = SrgbToLinear(srgb.b);

        float l = 0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b;
        float m = 0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b;
        float s = 0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b;

        float l_ = Mathf.Pow(l, 1f / 3f);
        float m_ = Mathf.Pow(m, 1f / 3f);
        float s_ = Mathf.Pow(s, 1f / 3f);

        L = 0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_;
        A = 1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_;
        B = 0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_;
    }

    private static float SrgbToLinear(float x)
    {
        if (x <= 0.04045f)
        {
            return x / 12.92f;
        }

        return Mathf.Pow((x + 0.055f) / 1.055f, 2.4f);
    }
}