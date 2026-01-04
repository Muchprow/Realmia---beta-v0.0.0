using System.Collections.Generic;
using System;
using System.Numerics;

namespace Realmia
{
    public class World
    {
        private readonly Perlin noise;
        private readonly Dictionary<(int, int), Chunk> chunks = new();
        private readonly int viewRadiusChunks;
        private readonly int unloadRadiusChunks;
        private readonly int barrierRadiusChunks;

        // streaming helpers
        private readonly Queue<(int, int)> pending = new();
        private readonly HashSet<(int, int)> pendingSet = new();
        private readonly int genPerFrame = 4;
        private readonly int syncLoadRadiusChunks = 2;

        public World(int seed = 0, int viewDistanceBlocks = 64, int barrierDistanceBlocks = 100)
        {
            noise = new Perlin(seed);
            TextureManager.LoadDefaults();

            viewRadiusChunks = Math.Max(2, (viewDistanceBlocks + Chunk.CHUNK_SIZE - 1) / Chunk.CHUNK_SIZE);
            unloadRadiusChunks = viewRadiusChunks + 2;
            barrierRadiusChunks = Math.Max(0, (barrierDistanceBlocks + Chunk.CHUNK_SIZE - 1) / Chunk.CHUNK_SIZE);

            for (int zx = -syncLoadRadiusChunks; zx <= syncLoadRadiusChunks; zx++)
            for (int zz = -syncLoadRadiusChunks; zz <= syncLoadRadiusChunks; zz++)
            {
                var c = new Chunk(zx, zz, noise);
                ApplyBarrierIfNeeded(c, zx, zz);
                chunks[(zx, zz)] = c;
            }

            for (int zx = -viewRadiusChunks; zx <= viewRadiusChunks; zx++)
            for (int zz = -viewRadiusChunks; zz <= viewRadiusChunks; zz++)
            {
                var key = (zx, zz);
                if (chunks.ContainsKey(key)) continue;
                pending.Enqueue(key);
                pendingSet.Add(key);
            }
        }

        public void Update(Vector3 playerPosition)
        {
            int px = (int)Math.Floor(playerPosition.X);
            int pz = (int)Math.Floor(playerPosition.Z);
            int pcx = FloorDiv(px, Chunk.CHUNK_SIZE);
            int pcz = FloorDiv(pz, Chunk.CHUNK_SIZE);

            for (int dx = -syncLoadRadiusChunks; dx <= syncLoadRadiusChunks; dx++)
            for (int dz = -syncLoadRadiusChunks; dz <= syncLoadRadiusChunks; dz++)
            {
                int cx = pcx + dx;
                int cz = pcz + dz;
                CreateChunkIfMissing(cx, cz);
            }

            int generated = 0;
            while (generated < genPerFrame && pending.Count > 0)
            {
                var key = pending.Dequeue();
                pendingSet.Remove(key);
                int cx = key.Item1, cz = key.Item2;
                if (chunks.ContainsKey((cx, cz))) continue;
                CreateChunkIfMissing(cx, cz);
                generated++;
            }

            var toRemove = new List<(int, int)>();
            foreach (var key in chunks.Keys)
            {
                int cx = key.Item1;
                int cz = key.Item2;
                if (Math.Abs(cx - pcx) > unloadRadiusChunks + 2 || Math.Abs(cz - pcz) > unloadRadiusChunks + 2)
                    toRemove.Add(key);
            }
            foreach (var k in toRemove) chunks.Remove(k);
        }

        private void CreateChunkIfMissing(int cx, int cz)
        {
            var key = (cx, cz);
            if (chunks.ContainsKey(key)) return;

            var c = new Chunk(cx, cz, noise);
            ApplyBarrierIfNeeded(c, cx, cz);
            chunks[key] = c;

            if (pendingSet.Remove(key)) { }
        }

        public void Draw(Vector3 viewerPosition)
        {
            int px = (int)Math.Floor(viewerPosition.X);
            int pz = (int)Math.Floor(viewerPosition.Z);
            int pcx = FloorDiv(px, Chunk.CHUNK_SIZE);
            int pcz = FloorDiv(pz, Chunk.CHUNK_SIZE);

            int drawRadiusChunks = Math.Min(2, viewRadiusChunks);

            foreach (var kv in chunks)
            {
                int cx = kv.Key.Item1;
                int cz = kv.Key.Item2;
                if (Math.Abs(cx - pcx) > drawRadiusChunks || Math.Abs(cz - pcz) > drawRadiusChunks) continue;
                kv.Value.Draw();
            }
        }

        public bool IsSolidAt(int x, int y, int z)
        {
            int cx = FloorDiv(x, Chunk.CHUNK_SIZE);
            int cz = FloorDiv(z, Chunk.CHUNK_SIZE);

            if (!chunks.TryGetValue((cx, cz), out var chunk))
                return false;

            if (!chunk.IsReady)
                return false;

            if (y < 0 || y >= Chunk.HEIGHT)
                return false;

            int lx = Mod(x, Chunk.CHUNK_SIZE);
            int lz = Mod(z, Chunk.CHUNK_SIZE);

            return chunk.Blocks[lx, y, lz].IsSolid;
        }

        private static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);
        private static int Mod(int a, int b) { int r = a % b; return r < 0 ? r + b : r; }

        private void ApplyBarrierIfNeeded(Chunk chunk, int cx, int cz)
        {
            if (barrierRadiusChunks <= 0) return;

            int absx = Math.Abs(cx);
            int absz = Math.Abs(cz);
            if (absx != barrierRadiusChunks && absz != barrierRadiusChunks) return;

            for (int y = 0; y < Chunk.HEIGHT; y++)
            {
                if (absx == barrierRadiusChunks)
                {
                    int localX = cx > 0 ? Chunk.CHUNK_SIZE - 1 : 0;
                    for (int z = 0; z < Chunk.CHUNK_SIZE; z++)
                        chunk.Blocks[localX, y, z].Type = BlockType.Deadend;
                }
                if (absz == barrierRadiusChunks)
                {
                    int localZ = cz > 0 ? Chunk.CHUNK_SIZE - 1 : 0;
                    for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
                        chunk.Blocks[x, y, localZ].Type = BlockType.Deadend;
                }
            }
        }

        public int LoadedChunkCount => chunks.Count;

        public int PendingCount => pending.Count;

        public bool HasChunk(int cx, int cz)
        {
        return chunks.ContainsKey((cx, cz));
        }
    }
}
