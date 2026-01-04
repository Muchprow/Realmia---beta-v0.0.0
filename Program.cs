using System;
using Raylib_cs;
using Realmia;
using static Raylib_cs.Raylib;
using System.Numerics;
using System.Reflection;

class Program
{
    const int CHUNK_SIZE = 16;
    const int WORLD_HEIGHT = 64;

    static Camera3D camera;
    static bool showHUD = true;

    static void Main()
    {
        // Настройки окна
        InitWindow(1280, 720, "Realmia");
        // Make window cover the primary monitor (windowed, not exclusive fullscreen)
        SetWindowMinSize(800, 600);
        int mw = GetMonitorWidth(0);
        int mh = GetMonitorHeight(0);
        SetWindowPosition(0, 0);
        SetWindowSize(mw, mh);

        SetTargetFPS(60);

        // World and player
        var world = new World(seed: 12345);
        var player = new Player(new Vector3(8, 40, 8));

        // Camera struct helper
        camera = new Camera3D();
        object boxed = camera;
        Type camType = typeof(Camera3D);
        void TrySet(string name, object value)
        {
            var f = camType.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f != null) { f.SetValue(boxed, value); return; }
            var p = camType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null && p.CanWrite) { p.SetValue(boxed, value); return; }
        }

        // F3 fallback
        object keyF3 = null;
        try { keyF3 = Enum.Parse(typeof(KeyboardKey), "F3", true); } catch { try { keyF3 = Enum.Parse(typeof(KeyboardKey), "KEY_F3", true); } catch { keyF3 = null; } }

        // lock mouse for look
        DisableCursor();

        while (!WindowShouldClose())
        {
            float dt = GetFrameTime();

            if (keyF3 != null && IsKeyPressed((KeyboardKey)keyF3)) showHUD = !showHUD;

            // update world streaming around player, then player physics
            world.Update(player.Position);
            player.Update(dt, world);

            // build camera from player
            float yawRad = MathF.PI * player.Yaw / 180f;
            float pitchRad = MathF.PI * player.Pitch / 180f;
            Vector3 forward = new Vector3(MathF.Cos(pitchRad) * MathF.Cos(yawRad), MathF.Sin(pitchRad), MathF.Cos(pitchRad) * MathF.Sin(yawRad));
            Vector3 camPos = player.Position + new Vector3(0, 0.4f, 0);
            Vector3 camTarget = camPos + forward;
            TrySet("position", camPos);
            TrySet("target", camTarget);
            TrySet("up", new Vector3(0, 1, 0));
            TrySet("fovy", 70.0f);
            camera = (Camera3D)boxed;

            BeginDrawing();
            ClearBackground(new Color(135, 206, 235, 255)); // sky

            BeginMode3D(camera);
            world.Draw(player.Position);
            EndMode3D();

            // HUD / debug (toggle with F3)
            if (showHUD)
            {
                DrawText("Realmia", 10, 10, 20, new Color(0, 0, 0, 255));
                DrawFPS(10, 40);
                var p = player.Position;
                DrawText($"Pos: {p.X:F1}, {p.Y:F1}, {p.Z:F1}", 10, 70, 16, new Color(0,0,0,255));
                DrawText($"OnGround: {player.OnGround}", 10, 90, 16, new Color(0,0,0,255));

                // world diagnostics
                try
                {
                    DrawText($"Chunks: {world.LoadedChunkCount}  Pending: {world.PendingCount}", 10, 110, 16, new Color(0,0,0,255));
                }
                catch { }

                // extra physics diagnostics
                try
                {
                    int px = (int)Math.Floor(p.X);
                    int pz = (int)Math.Floor(p.Z);
                    int pcx = (int)Math.Floor((double)px / Chunk.CHUNK_SIZE);
                    int pcz = (int)Math.Floor((double)pz / Chunk.CHUNK_SIZE);
                    DrawText($"Chunk: {pcx},{pcz} Loaded: {world.HasChunk(pcx, pcz)}", 10, 130, 16, new Color(0,0,0,255));
                    int footY = (int)Math.Floor(p.Y - 0.9f);
                    DrawText($"BelowSolid@{footY}: {world.IsSolidAt(px, footY, pz)}", 10, 150, 16, new Color(0,0,0,255));
                }
                catch { }

                // Temporary: draw a loaded texture to verify textures are correct
                if (TextureManager.TryGet(BlockType.Grass, out var grassTex))
                {
                    DrawTexture(grassTex, 10, 120, new Color(255, 255, 255, 255));
                    int texW = 0, texH = 0;
                    try
                    {
                        var tt = grassTex.GetType();
                        var fw = tt.GetField("width", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        var fh = tt.GetField("height", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (fw != null) texW = Convert.ToInt32(fw.GetValue(grassTex));
                        else
                        {
                            var pw = tt.GetProperty("width", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            if (pw != null) texW = Convert.ToInt32(pw.GetValue(grassTex));
                            else
                            {
                                var pW = tt.GetProperty("Width", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                                if (pW != null) texW = Convert.ToInt32(pW.GetValue(grassTex));
                            }
                        }
                        if (fh != null) texH = Convert.ToInt32(fh.GetValue(grassTex));
                        else
                        {
                            var ph = tt.GetProperty("height", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            if (ph != null) texH = Convert.ToInt32(ph.GetValue(grassTex));
                            else
                            {
                                var pH = tt.GetProperty("Height", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                                if (pH != null) texH = Convert.ToInt32(pH.GetValue(grassTex));
                            }
                        }
                    }
                    catch { }
                    DrawText($"Tex size: {texW}x{texH}", 10, 160, 14, new Color(0, 0, 0, 255));
                }
            }
            EndDrawing();
        }

        CloseWindow();
        // unload textures
        TextureManager.UnloadAll();
    }
}
