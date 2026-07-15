using UnityEngine;

/// <summary>
/// Seeded 2D Perlin noise with fractal (fBm) sampling.
///
/// Why not Mathf.PerlinNoise?
/// 1. It cannot be seeded, so different worlds would need coordinate offsets (hacky).
/// 2. This class is pure C# and thread-safe after construction, which lets
///    chunk generation run on background threads without touching Unity APIs.
/// 3. Fully deterministic: same seed + same coordinates = same value, always.
/// </summary>
public sealed class DeterministicNoise
{
    private readonly int[] perm = new int[512];

    public DeterministicNoise(int seed)
    {
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;

        // Fisher-Yates shuffle of the permutation table, driven by the seed.
        var rng = new System.Random(seed);
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    private static float Lerp(float a, float b, float t) => a + t * (b - a);

    private static float Grad(int hash, float x, float y)
    {
        switch (hash & 7)
        {
            case 0: return x + y;
            case 1: return -x + y;
            case 2: return x - y;
            case 3: return -x - y;
            case 4: return x;
            case 5: return -x;
            case 6: return y;
            default: return -y;
        }
    }

    /// <summary>Single octave. Returns roughly [-1, 1].</summary>
    public float Sample(float x, float y)
    {
        int xi = Mathf.FloorToInt(x);
        int yi = Mathf.FloorToInt(y);
        float xf = x - xi;
        float yf = y - yi;
        xi &= 255;
        yi &= 255;

        float u = Fade(xf);
        float v = Fade(yf);

        int aa = perm[perm[xi] + yi];
        int ab = perm[perm[xi] + yi + 1];
        int ba = perm[perm[xi + 1] + yi];
        int bb = perm[perm[xi + 1] + yi + 1];

        float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
        float x2 = Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
        return Lerp(x1, x2, v);
    }

    /// <summary>Fractal noise (multiple octaves). Returns [0, 1].</summary>
    public float Fbm(float x, float y, int octaves, float lacunarity, float persistence)
    {
        float sum = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float max = 0f;

        for (int o = 0; o < octaves; o++)
        {
            sum += Sample(x * frequency, y * frequency) * amplitude;
            max += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return (sum / max) * 0.5f + 0.5f;
    }
}