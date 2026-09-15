using Richman.Core;
using UnityEngine;

namespace Richman.Presentation
{
    public sealed class PawnView : MonoBehaviour
    {
        [SerializeField] private Renderer[] colorRenderers;
        [SerializeField] private TextMesh playerLabel;

        private MaterialPropertyBlock _propertyBlock;

        public int PlayerId { get; private set; }

        public void SetPrefabReferences(Renderer[] renderers, TextMesh label)
        {
            colorRenderers = renderers;
            playerLabel = label;
        }

        public void Configure(PlayerState player)
        {
            PlayerId = player.Id;
            if (playerLabel != null) playerLabel.text = "P" + player.Id;
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            if (colorRenderers == null) return;
            for (var i = 0; i < colorRenderers.Length; i++)
            {
                ToyCityPalette.ApplyColor(colorRenderers[i], ToyCityPalette.PlayerColor(player.Id), _propertyBlock);
            }
        }
    }
}
