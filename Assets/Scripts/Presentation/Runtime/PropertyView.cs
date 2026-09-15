using Richman.Core;
using UnityEngine;

namespace Richman.Presentation
{
    public sealed class PropertyView : MonoBehaviour
    {
        [SerializeField] private GameObject emptyLot;
        [SerializeField] private GameObject ownershipIndicator;
        [SerializeField] private GameObject[] levelVisuals;
        [SerializeField] private Renderer[] coloredRenderers;

        private MaterialPropertyBlock _propertyBlock;

        public void SetPrefabReferences(
            GameObject lot,
            GameObject indicator,
            GameObject[] levels,
            Renderer[] renderers)
        {
            emptyLot = lot;
            ownershipIndicator = indicator;
            levelVisuals = levels;
            coloredRenderers = renderers;
        }

        public void Apply(BoardTileDefinition definition, PropertyState property)
        {
            var owned = property != null && property.OwnerId.HasValue;
            var level = property == null ? 0 : property.UpgradeLevel;
            if (emptyLot != null) emptyLot.SetActive(true);
            if (ownershipIndicator != null) ownershipIndicator.SetActive(owned);

            if (levelVisuals != null)
            {
                for (var i = 0; i < levelVisuals.Length; i++)
                {
                    if (levelVisuals[i] != null) levelVisuals[i].SetActive(owned && level == i + 1);
                }
            }

            var color = owned ? ToyCityPalette.PlayerColor(property.OwnerId.Value) : ToyCityPalette.EmptyLotColor();
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            if (coloredRenderers != null)
            {
                for (var i = 0; i < coloredRenderers.Length; i++)
                {
                    ToyCityPalette.ApplyColor(coloredRenderers[i], color, _propertyBlock);
                }
            }
        }
    }
}
