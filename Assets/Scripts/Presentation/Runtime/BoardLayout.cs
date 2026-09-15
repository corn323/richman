using UnityEngine;

namespace Richman.Presentation
{
    public static class BoardLayout
    {
        public const int SideLength = 10;
        public const float TileSpacing = 1.35f;

        public static Vector3 TilePosition(int index)
        {
            int x;
            int z;
            if (index < SideLength)
            {
                x = index;
                z = 0;
            }
            else if (index < 2 * SideLength - 1)
            {
                x = SideLength - 1;
                z = index - (SideLength - 1);
            }
            else if (index < 3 * SideLength - 2)
            {
                x = SideLength - 1 - (index - (2 * SideLength - 2));
                z = SideLength - 1;
            }
            else
            {
                x = 0;
                z = SideLength - 1 - (index - (3 * SideLength - 3));
            }

            var centeredX = x - (SideLength - 1) * 0.5f;
            var centeredZ = z - (SideLength - 1) * 0.5f;
            return new Vector3(centeredX * TileSpacing, 0f, centeredZ * TileSpacing);
        }

        public static Vector3 PawnOffset(int playerId)
        {
            switch ((playerId - 1) % 4)
            {
                case 0: return new Vector3(-0.30f, 0.18f, -0.30f);
                case 1: return new Vector3(0.30f, 0.18f, -0.30f);
                case 2: return new Vector3(-0.30f, 0.18f, 0.30f);
                default: return new Vector3(0.30f, 0.18f, 0.30f);
            }
        }
    }
}
