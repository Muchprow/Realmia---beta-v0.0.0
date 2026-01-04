using System.Numerics;
using Raylib_cs;
using System.Reflection;

namespace Realmia
{
    public class Chunk
    {
        public const int CHUNK_SIZE = 16;
        public const int HEIGHT = 64;
        public Block[,,] Blocks = new Block[CHUNK_SIZE, HEIGHT, CHUNK_SIZE];
        public Vector3 Position;
        public bool IsReady { get; private set; } = false;

        public Chunk(int worldX, int worldZ, Perlin noise)
        {
            Position = new Vector3(worldX * CHUNK_SIZE, 0, worldZ * CHUNK_SIZE);
            Generate(noise);

            IsReady = true;
        }

        private void Generate(Perlin noise)
        {
            for (int x = 0; x < CHUNK_SIZE; x++)
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                float fx = (Position.X + x) * 0.05f;
                float fz = (Position.Z + z) * 0.05f;
                float n = noise.Noise(fx, fz);
                int h = (int)(n * 20) + 8; // height
                for (int y = 0; y < HEIGHT; y++)
                {
                    Block b = new Block();
                    if (y > h) b.Type = BlockType.Air;
                    else if (y == h) b.Type = BlockType.Grass;
                    else if (y > h - 3) b.Type = BlockType.Dirt;
                    else b.Type = BlockType.Stone;
                    Blocks[x, y, z] = b;
                }
            }
        }

        public void Draw()
        {
            for (int x = 0; x < CHUNK_SIZE; x++)
            for (int y = 0; y < HEIGHT; y++)
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                var b = Blocks[x, y, z];
                if (!b.IsSolid) continue;
                Vector3 pos = new Vector3(Position.X + x + 0.5f, y + 0.5f, Position.Z + z + 0.5f);
                // try textured draw first
                if (TextureManager.TryGet(b.Type, out var tex))
                {
                    // Try to get generated model for this block type
                    if (TextureManager.TryGetModel(b.Type, out var modelObj) && modelObj != null)
                    {
                        // call DrawModel(model, position, scale, tint)
                        var mdraw = typeof(Raylib).GetMethod("DrawModel", BindingFlags.Public | BindingFlags.Static);
                        if (mdraw != null)
                        {
                            try { mdraw.Invoke(null, new object[] { modelObj, pos, 1f, new Color(255, 255, 255, 255) }); }
                            catch { Raylib.DrawCube(pos, 1, 1, 1, new Color(200, 200, 200, 255)); }
                        }
                        else
                        {
                            Raylib.DrawCube(pos, 1, 1, 1, new Color(200, 200, 200, 255));
                        }
                    }
                    else
                    {
                        // no model created or model not textured: try DrawCubeTexture if available
                        var drawCubeTex = typeof(Raylib).GetMethod("DrawCubeTexture", BindingFlags.Public | BindingFlags.Static);
                        if (drawCubeTex != null)
                        {
                            try
                            {
                                // signature: DrawCubeTexture(Texture2D texture, Vector3 position, float width, float height, float length, Color tint)
                                drawCubeTex.Invoke(null, new object[] { tex, pos, 1f, 1f, 1f, new Color(255, 255, 255, 255) });
                            }
                            catch
                            {
                                Raylib.DrawCube(pos, 1, 1, 1, new Color(200, 200, 200, 255));
                            }
                        }
                        else
                        {
                            // no DrawCubeTexture available: fallback to solid cube
                            Raylib.DrawCube(pos, 1, 1, 1, new Color(200, 200, 200, 255));
                        }
                    }
                }
                else
                {
                    Color color = b.Type switch
                    {
                        BlockType.Grass => new Color(0, 200, 0, 255),
                        BlockType.Dirt => new Color(134, 83, 52, 255),
                        _ => new Color(128, 128, 128, 255)
                    };
                    Raylib.DrawCube(pos, 1, 1, 1, color);
                }
            }
        }
    }
}
