using System.Collections;
using System.Collections.Generic;
using Richman.Core;
using UnityEngine;

namespace Richman.Presentation
{
    public sealed class BoardView : MonoBehaviour
    {
        [Header("Scene-owned board hierarchy")]
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform tileRoot;
        [SerializeField] private Transform cityRoot;
        [SerializeField] private Transform pawnRoot;
        [SerializeField] private Transform diceRoot;

        [Header("Replaceable Prefabs")]
        [SerializeField] private BoardTileView tilePrefab;
        [SerializeField] private PropertyView propertyPrefab;
        [SerializeField] private PawnView pawnPrefab;
        [SerializeField] private DiceView dicePrefab;
        [SerializeField] private GameObject cityLandmarkPrefab;

        [Header("Scene instances")]
        [SerializeField] private BoardTileView[] tileViews;
        [SerializeField] private PawnView[] pawnViews;
        [SerializeField] private DiceView diceView;

        private GameSession _session;

        public DiceView Dice => diceView;

        public void SetSceneReferences(
            Transform board,
            Transform tiles,
            Transform city,
            Transform pawns,
            Transform dice,
            BoardTileView tileAsset,
            PropertyView propertyAsset,
            PawnView pawnAsset,
            DiceView diceAsset,
            GameObject cityAsset,
            BoardTileView[] sceneTiles,
            PawnView[] scenePawns,
            DiceView sceneDice)
        {
            boardRoot = board;
            tileRoot = tiles;
            cityRoot = city;
            pawnRoot = pawns;
            diceRoot = dice;
            tilePrefab = tileAsset;
            propertyPrefab = propertyAsset;
            pawnPrefab = pawnAsset;
            dicePrefab = diceAsset;
            cityLandmarkPrefab = cityAsset;
            tileViews = sceneTiles;
            pawnViews = scenePawns;
            diceView = sceneDice;
        }

        public void Bind(GameSession session)
        {
            _session = session;
            if (_session == null || tileViews == null) return;

            for (var i = 0; i < tileViews.Length && i < _session.Board.TileCount; i++)
            {
                if (tileViews[i] != null) tileViews[i].Configure(_session.Board.GetTileAt(i));
            }

            for (var i = 0; i < pawnViews.Length && i < _session.State.Players.Count; i++)
            {
                if (pawnViews[i] != null) pawnViews[i].Configure(_session.State.Players[i]);
            }

            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            if (_session == null || tileViews == null) return;
            for (var i = 0; i < tileViews.Length; i++)
            {
                if (tileViews[i] != null) tileViews[i].ApplyState(_session.State);
            }

            for (var i = 0; i < pawnViews.Length; i++)
            {
                var pawn = pawnViews[i];
                if (pawn == null) continue;
                var player = _session.State.GetPlayer(pawn.PlayerId);
                if (player != null) pawn.transform.position = PawnWorldPosition(player.CurrentTileIndex, player.Id);
            }
        }

        public PawnView GetPawn(int playerId)
        {
            if (pawnViews == null) return null;
            for (var i = 0; i < pawnViews.Length; i++)
            {
                if (pawnViews[i] != null && pawnViews[i].PlayerId == playerId) return pawnViews[i];
            }

            return null;
        }

        public Transform GetTileTransform(int tileIndex)
        {
            return tileViews[tileIndex].transform;
        }

        public Vector3 PawnWorldPosition(int tileIndex, int playerId)
        {
            return tileViews[tileIndex].PawnAnchorPosition + BoardLayout.PawnOffset(playerId);
        }

        public IEnumerator AnimatePawn(int playerId, int fromTileIndex, int stepCount)
        {
            var pawn = GetPawn(playerId);
            if (pawn == null || _session == null) yield break;

            var start = pawn.transform.position;
            for (var step = 1; step <= stepCount; step++)
            {
                var tileIndex = (fromTileIndex + step) % _session.Board.TileCount;
                var target = PawnWorldPosition(tileIndex, playerId);
                yield return MovePawn(pawn.transform, start, target, 0.16f);
                start = target;
            }

            var player = _session.State.GetPlayer(playerId);
            if (player != null) pawn.transform.position = PawnWorldPosition(player.CurrentTileIndex, player.Id);
        }

        private static IEnumerator MovePawn(Transform pawn, Vector3 from, Vector3 to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var position = Vector3.Lerp(from, to, t);
                position.y += Mathf.Sin(t * Mathf.PI) * 0.42f;
                pawn.position = position;
                yield return null;
            }

            pawn.position = to;
        }
    }
}
