using Richman.Core;
using UnityEngine;

namespace Richman.Presentation
{
    public sealed class BoardTileView : MonoBehaviour
    {
        [SerializeField] private Renderer surfaceRenderer;
        [SerializeField] private Renderer trimRenderer;
        [SerializeField] private TextMesh label;
        [SerializeField] private Transform pawnAnchor;
        [SerializeField] private Transform propertyAnchor;
        [SerializeField] private PropertyView propertyView;

        private MaterialPropertyBlock _propertyBlock;

        public int BoardIndex { get; private set; }
        public PropertyView PropertyView => propertyView;
        public Transform PropertyAnchor => propertyAnchor;
        public Vector3 PawnAnchorPosition => pawnAnchor == null ? transform.position + Vector3.up * 0.18f : pawnAnchor.position;

        public void SetScenePropertyView(PropertyView propertySlot)
        {
            propertyView = propertySlot;
        }

        public void SetPrefabReferences(
            Renderer surface,
            Renderer trim,
            TextMesh tileLabel,
            Transform pawn,
            Transform property,
            PropertyView propertySlot)
        {
            surfaceRenderer = surface;
            trimRenderer = trim;
            label = tileLabel;
            pawnAnchor = pawn;
            propertyAnchor = property;
            propertyView = propertySlot;
        }

        public void Configure(BoardTileDefinition definition)
        {
            BoardIndex = definition.PositionIndex;
            if (label != null) label.text = definition.PositionIndex + "\n" + ShortName(definition.DisplayName);
        }

        public void ApplyState(GameState state)
        {
            var definition = state.Board.GetTileAt(BoardIndex);
            var property = state.GetProperty(definition.Id);
            var color = GetSurfaceColor(definition, property);
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            ToyCityPalette.ApplyColor(surfaceRenderer, color, _propertyBlock);
            ToyCityPalette.ApplyColor(trimRenderer, Color.Lerp(color, Color.black, 0.35f), _propertyBlock);
            if (propertyView != null) propertyView.Apply(definition, property);

            if (label != null)
            {
                var text = definition.PositionIndex + "\n" + ShortName(definition.DisplayName);
                if (property != null && property.OwnerId.HasValue)
                {
                    text += "\nP" + property.OwnerId.Value + " L" + property.UpgradeLevel;
                }

                label.text = text;
            }
        }

        private static Color GetSurfaceColor(BoardTileDefinition definition, PropertyState property)
        {
            if (property != null && property.OwnerId.HasValue)
            {
                return Color.Lerp(ToyCityPalette.PlayerColor(property.OwnerId.Value), Color.white, 0.16f);
            }

            if (definition.Type == BoardTileType.Start) return new Color(0.13f, 0.55f, 0.77f);
            if (definition.Type != BoardTileType.Property) return ToyCityPalette.NeutralTileColor();
            return ToyCityPalette.DistrictColor(definition.DistrictId);
        }

        private static string ShortName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "Tile";
            return displayName.Length <= 12 ? displayName : displayName.Substring(0, 12);
        }
    }
}
