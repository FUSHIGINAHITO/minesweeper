using System.Collections.Generic;
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

            result[i] = OklchToSrgbWithGamutFit(so.lightness, so.chroma, h, so.alpha);
        }

        return result;
    }

    private static Color OklchToSrgbWithGamutFit(float l, float c, float h01, float alpha)
    {
        // 二分收缩 chroma，尽量保持色相，同时保证在 sRGB 色域内
        float lo = 0f;
        float hi = Mathf.Max(0f, c);

        Color best = OklchToSrgb(l, 0f, h01, alpha);
        for (int i = 0; i < 14; i++)
        {
            float mid = (lo + hi) * 0.5f;
            Color candidate = OklchToSrgb(l, mid, h01, alpha);

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

    private static Color OklchToSrgb(float l, float c, float h01, float alpha)
    {
        float angle = h01 * Mathf.PI * 2f;
        float a = c * Mathf.Cos(angle);
        float b = c * Mathf.Sin(angle);

        // OKLab -> LMS'
        float l_ = l + 0.3963377774f * a + 0.2158037573f * b;
        float m_ = l - 0.1055613458f * a - 0.0638541728f * b;
        float s_ = l - 0.0894841775f * a - 1.2914855480f * b;

        // LMS' -> LMS
        float l3 = l_ * l_ * l_;
        float m3 = m_ * m_ * m_;
        float s3 = s_ * s_ * s_;

        // LMS -> Linear sRGB
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
}