using System;
using System.Collections;
using Richman.Core;
using Richman.Presentation;
using UnityEngine;

namespace Richman.Playtest
{
    public sealed class PlaytestGame : MonoBehaviour
    {
        [Header("Gameplay Scene References")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private GameplayHUD gameplayHud;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private DiceView diceView;
        [SerializeField] private int seed;

        private GameSession _session;
        private bool _presentationBusy;
        private string _message;

        public void SetSceneReferences(BoardView board, GameplayHUD hud, CameraRig camera, DiceView dice)
        {
            boardView = board;
            gameplayHud = hud;
            cameraRig = camera;
            diceView = dice;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (boardView == null) boardView = FindFirstObjectByType<BoardView>();
            if (gameplayHud == null) gameplayHud = FindFirstObjectByType<GameplayHUD>();
            if (cameraRig == null) cameraRig = FindFirstObjectByType<CameraRig>();
            if (diceView == null && boardView != null) diceView = boardView.Dice;

            StartNewGame();
        }

        private void StartNewGame()
        {
            StopAllCoroutines();
            _presentationBusy = false;
            var actualSeed = seed == 0 ? Environment.TickCount : seed;
            _session = PrototypeGameFactory.CreateFourPlayerSession(actualSeed);
            _message = RichmanLocalization.Text("welcome");

            if (boardView != null) boardView.Bind(_session);
            if (gameplayHud != null)
            {
                gameplayHud.BindActions(RollDice, BuyProperty, UpgradeProperty, ChooseRoute, EndTurn, StartNewGame, SetLanguage);
                gameplayHud.Render(_session.State, _message, false);
            }

            if (cameraRig != null) cameraRig.FocusOverview();
        }

        private void RollDice()
        {
            if (!CanAcceptAction()) return;
            var current = _session.State.GetPlayer(_session.State.CurrentPlayerId);
            var result = _session.Execute(new RollDiceCommand(current.Id));
            if (!result.Succeeded)
            {
                ShowError(RichmanLocalization.CommandError(result.ErrorCode, result.ErrorMessage));
                return;
            }

            if (result.DiceResult == null)
            {
                _message = _session.State.LastEventMessage;
                if (boardView != null) boardView.RefreshVisuals();
                RenderHud();
                return;
            }

            _message = RichmanLocalization.Format("rolled", current.Id, result.DiceResult.Total);
            _presentationBusy = true;
            if (boardView != null) boardView.RefreshVisuals();
            RenderHud();
            StartCoroutine(PlayRollAndMovement(current.Id, result.DiceResult, result.PathTileIndices));
        }

        private IEnumerator PlayRollAndMovement(int playerId, DiceResult result, System.Collections.Generic.IReadOnlyList<int> path)
        {
            var pawn = boardView == null ? null : boardView.GetPawn(playerId);
            if (cameraRig != null && pawn != null) cameraRig.Focus(pawn.transform);
            yield return new WaitForSeconds(0.25f);

            if (cameraRig != null && diceView != null) cameraRig.Focus(diceView.transform);
            if (diceView != null) yield return diceView.PlayRoll(result);
            if (cameraRig != null && pawn != null) cameraRig.Focus(pawn.transform);
            if (boardView != null) yield return boardView.AnimatePawn(playerId, path);

            _presentationBusy = false;
            _message = string.IsNullOrEmpty(_session.State.LastEventMessage)
                ? DescribeLanding(playerId)
                : _session.State.LastEventMessage;
            if (boardView != null) boardView.RefreshVisuals();
            RenderHud();
        }

        private void BuyProperty()
        {
            ExecuteSimple(new BuyPropertyCommand(_session.State.CurrentPlayerId), RichmanLocalization.Text("purchased"));
        }

        private void UpgradeProperty(string propertyId)
        {
            ExecuteSimple(new UpgradePropertyCommand(_session.State.CurrentPlayerId, propertyId), RichmanLocalization.Text("upgraded"));
        }

        private void ChooseRoute(string nextTileId)
        {
            if (!CanAcceptAction()) return;
            var playerId = _session.State.CurrentPlayerId;
            var result = _session.Execute(new ChooseRouteCommand(playerId, nextTileId));
            if (!result.Succeeded)
            {
                ShowError(RichmanLocalization.CommandError(result.ErrorCode, result.ErrorMessage));
                return;
            }

            _presentationBusy = true;
            if (boardView != null) boardView.RefreshVisuals();
            RenderHud();
            StartCoroutine(PlayChosenRoute(playerId, result.PathTileIndices));
        }

        private IEnumerator PlayChosenRoute(int playerId, System.Collections.Generic.IReadOnlyList<int> path)
        {
            if (boardView != null) yield return boardView.AnimatePawn(playerId, path);

            _presentationBusy = false;
            _message = string.IsNullOrEmpty(_session.State.LastEventMessage)
                ? DescribeLanding(playerId)
                : _session.State.LastEventMessage;
            if (boardView != null) boardView.RefreshVisuals();
            RenderHud();
        }

        private void EndTurn()
        {
            ExecuteSimple(new EndTurnCommand(_session.State.CurrentPlayerId), "Turn ended.");
            if (cameraRig != null && boardView != null)
            {
                var pawn = boardView.GetPawn(_session.State.CurrentPlayerId);
                if (pawn != null) cameraRig.Focus(pawn.transform);
            }
        }

        private void ExecuteSimple(GameCommand command, string successMessage)
        {
            if (!CanAcceptAction()) return;
            var result = _session.Execute(command);
            if (!result.Succeeded)
            {
                ShowError(RichmanLocalization.CommandError(result.ErrorCode, result.ErrorMessage));
                return;
            }

            _message = _session.State.Phase == TurnPhase.Finished
                ? RichmanLocalization.Format("finished", _session.State.Outcome.WinnerId)
                : successMessage;
            if (boardView != null) boardView.RefreshVisuals();
            RenderHud();
        }

        private bool CanAcceptAction()
        {
            return !_presentationBusy && _session != null && _session.State.Outcome == null;
        }

        private string DescribeLanding(int playerId)
        {
            var player = _session.State.GetPlayer(playerId);
            var tile = _session.State.Board.GetTileAt(player.CurrentTileIndex);
            if (_session.State.Phase == TurnPhase.Finished)
            {
                return RichmanLocalization.Format("landed-winner", playerId, RichmanLocalization.TileName(tile), _session.State.Outcome.WinnerId);
            }

            if (_session.State.PendingAction == PendingAction.BuyProperty) return RichmanLocalization.Format("landed-buy", RichmanLocalization.TileName(tile));
            if (_session.State.PendingAction == PendingAction.UpgradeProperty) return RichmanLocalization.Format("landed-upgrade", RichmanLocalization.TileName(tile));
            return RichmanLocalization.Format("landed", playerId, RichmanLocalization.TileName(tile));
        }

        private void ShowError(string error)
        {
            _message = RichmanLocalization.Format("action-unavailable", error);
            RenderHud();
        }

        private void SetLanguage(RichmanLanguage language)
        {
            RichmanLocalization.SetLanguage(language);
            RenderHud();
        }

        private void RenderHud()
        {
            if (gameplayHud != null) gameplayHud.Render(_session.State, _message, _presentationBusy);
        }
    }
}
