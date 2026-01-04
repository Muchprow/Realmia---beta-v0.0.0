using System;

namespace Realmia
{
    // Simple Perlin noise implementation (2D)
    public class Perlin
    {
        private readonly int[] p;

        public Perlin(int seed = 0)
        {
            p = new int[512];
            var rnd = new Random(seed);
            var permutation = new int[256];
            for (int i = 0; i < 256; i++) permutation[i] = i;
            for (int i = 255; i > 0; i--) { int j = rnd.Next(i + 1); var t = permutation[i]; permutation[i] = permutation[j]; permutation[j] = t; }
            for (int i = 0; i < 512; i++) p[i] = permutation[i & 255];
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float a, float b, float t) => a + t * (b - a);
        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 7;
            float u = h < 4 ? x : y;
            float v = h < 4 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        public float Noise(float x, float y)
        {
            int X = FastFloor(x) & 255;
            int Y = FastFloor(y) & 255;
            x -= FastFloor(x);
            y -= FastFloor(y);
            float u = Fade(x);
            float v = Fade(y);
            int aa = p[p[X] + Y];
            int ab = p[p[X] + Y + 1];
            int ba = p[p[X + 1] + Y];
            int bb = p[p[X + 1] + Y + 1];
            float res = Lerp(Lerp(Grad(aa, x, y), Grad(ba, x - 1, y), u), Lerp(Grad(ab, x, y - 1), Grad(bb, x - 1, y - 1), u), v);
            return (res + 1) / 2; // normalize to 0..1
        }

        private static int FastFloor(float x) => (int)(x >= 0 ? x : x - 1);
    }
}
