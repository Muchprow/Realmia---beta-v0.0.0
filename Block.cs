namespace Realmia
{
    public enum BlockType : byte { Air = 0, Grass = 1, Dirt = 2, Stone = 3, Deadend = 4 }

    public struct Block
    {
        public BlockType Type;
        public bool IsSolid => Type != BlockType.Air;
    }
}
