using Godot;
using Richman.Core;

namespace Richman.GodotPresentation
{
    public partial class RichmanMain : Node3D
    {
        private GodotBoardView _board;
        private GodotDiceView _dice;
        private GodotCameraRig _camera;
        private GodotGameplayHud _hud;
        private GameSession _session;
        private bool _busy;
        private int _lastLandingPlayerId;

        public override void _Ready()
        {
            BuildPresentationBootstrap();
            _session = PrototypeGameFactory.CreateFourPlayerSession(928371);
            _board = new GodotBoardView { Name = "Board" };
            AddChild(_board);
            _board.Build(_session.Board, _session.State.Players);
            _dice = new GodotDiceView { Name = "Dice" };
            AddChild(_dice);
            _dice.Build();
            _camera = new GodotCameraRig { Name = "CameraRig" };
            AddChild(_camera);
            _camera.FocusOverview();
            _hud = new GodotGameplayHud { Name = "GameplayHUD" };
            AddChild(_hud);
            _hud.Build(Roll, Buy, Upgrade, EndTurn, ChooseRoute);
            _board.ApplyState(_session.State);
            _board.SyncPawns(_session.State);
            _hud.Render(_session.State, "Player 1, roll the dice.", false);
            if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--test-pawn-move") >= 0)
                _ = RunPawnMoveSmoke();
        }

        private async System.Threading.Tasks.Task RunPawnMoveSmoke()
        {
            try
            {
                var player = _session.State.GetPlayer(1);
                var result = _session.Execute(new RollDiceCommand(player.Id));
                if (!result.Succeeded || result.DiceResult == null || result.PathTileIndices.Count == 0)
                    throw new System.InvalidOperationException("Core did not produce a pawn movement path.");

                await _board.AnimatePawn(player.Id, result.PathTileIndices);
                if (result.PathTileIndices[result.PathTileIndices.Count - 1] != player.CurrentTileIndex)
                    await _board.AnimateTransfer(player.Id, player.CurrentTileIndex);

                var expected = _board.GetTilePosition(player.CurrentTileIndex) + new Vector3(-0.11f, 0.18f, -0.11f);
                if (_board.GetPawn(player.Id).Position.DistanceTo(expected) > 0.001f)
                    throw new System.InvalidOperationException("Animated pawn did not finish at the Core tile centre.");

                GD.Print("GODOT_PAWN_MOVE_OK from=start to=", _session.Board.GetTileAt(player.CurrentTileIndex).Id,
                    " steps=", result.PathTileIndices.Count, " trigger=", _session.State.LastEventMessage);
                GetTree().Quit();
            }
            catch (System.Exception exception)
            {
                GD.PushError("GODOT_PAWN_MOVE_FAILED " + exception);
                GetTree().Quit(1);
            }
        }

        private void BuildPresentationBootstrap()
        {
            var environment = new WorldEnvironment
            {
                Environment = new Environment
                {
                    BackgroundMode = Environment.BGMode.Color,
                    BackgroundColor = new Color("#101a2c"),
                    AmbientLightSource = Environment.AmbientSource.Color,
                    AmbientLightColor = new Color("#b9cfff"),
                    AmbientLightEnergy = 0.65f,
                    TonemapMode = Environment.ToneMapper.Filmic
                }
            };
            AddChild(environment);

            var light = new DirectionalLight3D
            {
                RotationDegrees = new Vector3(-52f, -28f, 0f),
                LightColor = new Color("#fff1d1"),
                LightEnergy = 1.2f,
                ShadowEnabled = true
            };
            AddChild(light);

        }

        private async void Roll()
        {
            if (_busy || _session.State.Outcome != null) return;
            var player = _session.State.GetPlayer(_session.State.CurrentPlayerId);
            var result = _session.Execute(new RollDiceCommand(player.Id));
            if (!result.Succeeded) { _hud.Render(_session.State, result.ErrorMessage, false); return; }
            if (result.DiceResult == null)
            {
                _lastLandingPlayerId = player.Id;
                _board.ApplyState(_session.State);
                _busy = false;
                _hud.Render(_session.State, _session.State.LastEventMessage, false);
                return;
            }
            _busy = true;
            _hud.Render(_session.State, "Rolling...", true);
            _camera.Focus(_dice.GlobalPosition, 10f);
            await _dice.Roll(result.DiceResult);
            _camera.Focus(_board.GetPawn(player.Id).GlobalPosition, 10f);
            await _board.AnimatePawn(player.Id, result.PathTileIndices);
            if (result.PathTileIndices.Count > 0 && result.PathTileIndices[result.PathTileIndices.Count - 1] != player.CurrentTileIndex)
            {
                await _board.AnimateTransfer(player.Id, player.CurrentTileIndex);
                _camera.Focus(_board.GetPawn(player.Id).GlobalPosition, 10f);
            }
            if (_session.State.LastDrawnCard != null)
            {
                GD.Print("CARD_DRAWN ", _session.State.LastDrawnCard.Id, " ", _session.State.LastDrawnCard.Description);
            }
            else
            {
                var landedTile = _session.Board.GetTileAt(player.CurrentTileIndex);
                if (landedTile.Type == BoardTileType.Airport || landedTile.Type == BoardTileType.Hospital || landedTile.Type == BoardTileType.Prison || landedTile.Type == BoardTileType.Court)
                    GD.Print("SPECIAL_TILE_RESOLVED ", landedTile.Id, " ", _session.State.LastEventMessage);
            }
            _lastLandingPlayerId = player.Id;
            _board.ApplyState(_session.State);
            _busy = false;
            var eventMessage = _session.State.LastEventMessage;
            _hud.Render(_session.State, string.IsNullOrEmpty(eventMessage) ? "Landed on " + _session.State.Board.GetTileAt(player.CurrentTileIndex).DisplayName : eventMessage, false);
        }

        private async void ChooseRoute(string nextTileId)
        {
            if (_busy || _session.State.Outcome != null) return;
            var playerId = _session.State.CurrentPlayerId;
            var result = _session.Execute(new ChooseRouteCommand(playerId, nextTileId));
            if (!result.Succeeded) { _hud.Render(_session.State, result.ErrorMessage, false); return; }
            _busy = true;
            _hud.Render(_session.State, "沿所選路線前進…", true);
            await _board.AnimatePawn(playerId, result.PathTileIndices);
            _camera.Focus(_board.GetPawn(playerId).GlobalPosition, 10f);
            _board.ApplyState(_session.State);
            _busy = false;
            var landing = _session.State.Board.GetTileAt(_session.State.GetPlayer(playerId).CurrentTileIndex);
            _hud.Render(_session.State, string.IsNullOrEmpty(_session.State.LastEventMessage) ? "抵達 " + landing.DisplayName : _session.State.LastEventMessage, false);
        }

        private void Buy()
        {
            Execute(new BuyPropertyCommand(_session.State.CurrentPlayerId), "Property purchased.");
        }

        private void Upgrade(string propertyId)
        {
            Execute(new UpgradePropertyCommand(_session.State.CurrentPlayerId, propertyId), "Property upgraded.");
        }

        private void EndTurn()
        {
            Execute(new EndTurnCommand(_session.State.CurrentPlayerId), "Turn ended.");
            _camera.Focus(_board.GetPawn(_session.State.CurrentPlayerId).GlobalPosition, 12f);
        }

        private void Execute(GameCommand command, string message)
        {
            if (_busy || _session.State.Outcome != null) return;
            var result = _session.Execute(command);
            if (!result.Succeeded) message = result.ErrorMessage;
            _board.ApplyState(_session.State);
            _hud.Render(_session.State, message, false);
        }
    }
}
