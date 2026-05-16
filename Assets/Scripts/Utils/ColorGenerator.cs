using UnityEngine;

public class ColorGenerator : ScriptableObject
{
    public static Color[] GeneratePerceptualHueCycleColors(MainDataSO so, int colorCount)
    {
        if (colorCount < 1)
        {
            colorCount = 1;
        }

        var result = new Color[colorCount];
        float hueStep = 1f / colorCount;

        float startHue = Random.value;
        bool reverse = Random.value > 0.5f;

        for (int i = 0; i < colorCount; i++)
        {
            float h = reverse ? startHue - i * hueStep : startHue + i * hueStep;
            h = h - Mathf.Floor(h);

            result[i] = OklchToSrgb(lightness: so.lightness, chroma: so.chroma, hue01: h, alpha: so.alpha);
        }

        return result;
    }

    public static Color OklchToSrgb(float lightness, float chroma, float hue01, float alpha = 1f)
    {
        return OklchToSrgbWithGamutFit(lightness, chroma, hue01, alpha);
    }

    public static void SrgbToOklch(Color srgb, out float lightness, out float chroma, out float hue01)
    {
        float r = SrgbToLinear(srgb.r);
        float g = SrgbToLinear(srgb.g);
        float b = SrgbToLinear(srgb.b);

        float l = 0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b;
        float m = 0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b;
        float s = 0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b;

        float l_ = Mathf.Pow(Mathf.Max(0f, l), 1f / 3f);
        float m_ = Mathf.Pow(Mathf.Max(0f, m), 1f / 3f);
        float s_ = Mathf.Pow(Mathf.Max(0f, s), 1f / 3f);

        float L = 0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_;
        float A = 1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_;
        float B = 0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_;

        lightness = Mathf.Clamp01(L);
        chroma = Mathf.Sqrt(A * A + B * B);

        float angle = Mathf.Atan2(B, A);
        hue01 = angle / (Mathf.PI * 2f);
        hue01 = hue01 - Mathf.Floor(hue01);
    }

    private static Color OklchToSrgbWithGamutFit(float l, float c, float h01, float alpha)
    {
        float lo = 0f;
        float hi = Mathf.Max(0f, c);

        Color best = OklchToSrgbUnsafe(l, 0f, h01, alpha);
        for (int i = 0; i < 14; i++)
        {
            float mid = (lo + hi) * 0.5f;
            Color candidate = OklchToSrgbUnsafe(l, mid, h01, alpha);

            if (candidate.r >= 0f && candidate.r <= 1f &&
                candidate.g >= 0f && candidate.g <= 1f &&
                candidate.b >= 0f && candidate.b <= 1f)
            {
                best = candidate;
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        best.a = alpha;
        return best;
    }

    private static Color OklchToSrgbUnsafe(float l, float c, float h01, float alpha)
    {
        float angle = h01 * Mathf.PI * 2f;
        float a = c * Mathf.Cos(angle);
        float b = c * Mathf.Sin(angle);

        float l_ = l + 0.3963377774f * a + 0.2158037573f * b;
        float m_ = l - 0.1055613458f * a - 0.0638541728f * b;
        float s_ = l - 0.0894841775f * a - 1.2914855480f * b;

        float l3 = l_ * l_ * l_;
        float m3 = m_ * m_ * m_;
        float s3 = s_ * s_ * s_;

        float rLin = 4.0767416621f * l3 - 3.3077115913f * m3 + 0.2309699292f * s3;
        float gLin = -1.2684380046f * l3 + 2.6097574011f * m3 - 0.3413193965f * s3;
        float bLin = -0.0041960863f * l3 - 0.7034186147f * m3 + 1.7076147010f * s3;

        float r = LinearToSrgb(rLin);
        float g = LinearToSrgb(gLin);
        float bl = LinearToSrgb(bLin);

        return new Color(r, g, bl, alpha);
    }

    private static float LinearToSrgb(float x)
    {
        if (x <= 0.0031308f)
        {
            return 12.92f * x;
        }

        return 1.055f * Mathf.Pow(x, 1f / 2.4f) - 0.055f;
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