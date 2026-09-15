using UnityEngine;

namespace Richman.Presentation
{
    public static class ToyCityPalette
    {
        private static readonly Color[] Players =
        {
            new Color(0.94f, 0.22f, 0.20f),
            new Color(0.17f, 0.49f, 0.94f),
            new Color(0.12f, 0.70f, 0.32f),
            new Color(0.96f, 0.60f, 0.08f)
        };

        private static readonly Color[] Districts =
        {
            new Color(0.74f, 0.31f, 0.39f),
            new Color(0.29f, 0.55f, 0.83f),
            new Color(0.28f, 0.69f, 0.52f),
            new Color(0.78f, 0.51f, 0.25f),
            new Color(0.57f, 0.39f, 0.76f),
            new Color(0.22f, 0.65f, 0.72f)
        };

        public static Color PlayerColor(int playerId)
        {
            return Players[Mathf.Abs(playerId - 1) % Players.Length];
        }

        public static Color DistrictColor(string districtId)
        {
            var hash = 17;
            if (!string.IsNullOrEmpty(districtId))
            {
                for (var i = 0; i < districtId.Length; i++) hash = hash * 31 + districtId[i];
            }

            return Districts[Mathf.Abs(hash) % Districts.Length];
        }

        public static Color NeutralTileColor()
        {
            return new Color(0.30f, 0.34f, 0.43f);
        }

        public static Color EmptyLotColor()
        {
            return new Color(0.67f, 0.73f, 0.78f);
        }

        public static void ApplyColor(Renderer renderer, Color color, MaterialPropertyBlock block)
        {
            if (renderer == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            var material = renderer.sharedMaterial;
            if (material == null || material.HasProperty("_BaseColor")) block.SetColor("_BaseColor", color);
            if (material == null || material.HasProperty("_Color")) block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }
    }
}
