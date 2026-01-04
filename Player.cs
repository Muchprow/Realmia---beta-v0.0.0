using System.Numerics;
using Raylib_cs;

namespace Realmia
{
    public class Player
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw = 0; // degrees
        public float Pitch = 0;
        public bool OnGround = false;

        public Player(Vector3 start)
        {
            Position = start;
            Velocity = Vector3.Zero;
        }

        public void Update(float dt, World world)
        {
            const float speed = 6f;
            const float jump = 6f;
            const float gravity = -20f;

            // mouse look
            var md = Raylib.GetMouseDelta();
            Yaw += md.X * 0.15f;
            Pitch -= md.Y * 0.15f;
            Pitch = Clamp(Pitch, -89f, 89f);

            // compute forward/right
            float yawRad = MathF.PI * Yaw / 180f;
            Vector3 forward = new Vector3(MathF.Cos(yawRad), 0, MathF.Sin(yawRad));
            // Right vector: use cross product to match engine coordinate system
            Vector3 right = Vector3.Cross(forward, Vector3.UnitY);

            Vector3 wish = Vector3.Zero;
            // robust key parsing for different Raylib-cs versions
            var keyW = ParseKey("W", "KEY_W");
            var keyS = ParseKey("S", "KEY_S");
            var keyD = ParseKey("D", "KEY_D");
            var keyA = ParseKey("A", "KEY_A");
            if (keyW.HasValue && Raylib.IsKeyDown(keyW.Value)) wish += forward;
            if (keyS.HasValue && Raylib.IsKeyDown(keyS.Value)) wish -= forward;
            if (keyD.HasValue && Raylib.IsKeyDown(keyD.Value)) wish += right;
            if (keyA.HasValue && Raylib.IsKeyDown(keyA.Value)) wish -= right;
            if (wish.Length() > 0) wish = Vector3.Normalize(wish) * speed;

            // horizontal velocity
            Velocity.X = wish.X;
            Velocity.Z = wish.Z;

            // gravity
            Velocity.Y += gravity * dt;

            // jump
            var keySpace = ParseKey("SPACE", "KEY_SPACE");
            if (OnGround && keySpace.HasValue && Raylib.IsKeyPressed(keySpace.Value)) { Velocity.Y = jump; OnGround = false; }

            // integrate
            Vector3 next = Position + Velocity * dt;

            // simple AABB collision test with blocks (player size: 0.6 x 1.8)
            float radius = 0.3f;
            float halfHeight = 0.9f;

            // check Y separately
            if (Velocity.Y <= 0)
            {
                // falling: check below
                int minX = (int)System.Math.Floor(next.X - radius);
                int maxX = (int)System.Math.Floor(next.X + radius);
                int minZ = (int)System.Math.Floor(next.Z - radius);
                int maxZ = (int)System.Math.Floor(next.Z + radius);
                int footY = (int)System.Math.Floor(next.Y - halfHeight);
                bool collided = false;
                for (int x = minX; x <= maxX && !collided; x++)
                for (int z = minZ; z <= maxZ && !collided; z++)
                {
                    if (world.IsSolidAt(x, footY, z)) collided = true;
                }
                if (collided)
                {
                    // place on top of block
                    next.Y = footY + 1 + halfHeight;
                    Velocity.Y = 0;
                    OnGround = true;
                }
                else OnGround = false;
            }

            // simple XZ collision (prevent walking through blocks)
            int checkY = (int)System.Math.Floor(next.Y - halfHeight + 0.01f);
            int minX2 = (int)System.Math.Floor(next.X - radius);
            int maxX2 = (int)System.Math.Floor(next.X + radius);
            int minZ2 = (int)System.Math.Floor(next.Z - radius);
            int maxZ2 = (int)System.Math.Floor(next.Z + radius);
            bool blocked = false;
            for (int x = minX2; x <= maxX2 && !blocked; x++)
            for (int z = minZ2; z <= maxZ2 && !blocked; z++) if (world.IsSolidAt(x, checkY, z)) blocked = true;
            if (blocked)
            {
                // cancel horizontal movement
                next.X = Position.X;
                next.Z = Position.Z;
                Velocity.X = 0; Velocity.Z = 0;
            }

            Position = next;
        }

        private static float Clamp(float v, float a, float b)
        {
            if (v < a) return a;
            if (v > b) return b;
            return v;
        }

        private static KeyboardKey? ParseKey(params string[] names)
        {
            foreach (var n in names)
            {
                if (Enum.TryParse<KeyboardKey>(n, true, out var k)) return k;
            }
            return null;
        }
    }
}
