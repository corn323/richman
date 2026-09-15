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
            _hud.Build(Roll, Buy, Upgrade, EndTurn);
            _board.ApplyState(_session.State);
            _board.SyncPawns(_session.State);
            _hud.Render(_session.State, "Player 1, roll the dice.", false);
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
            var from = player.CurrentTileIndex;
            var result = _session.Execute(new RollDiceCommand(player.Id));
            if (!result.Succeeded) { _hud.Render(_session.State, result.ErrorMessage, false); return; }
            _busy = true;
            _hud.Render(_session.State, "Rolling...", true);
            _camera.Focus(_dice.GlobalPosition, 10f);
            await _dice.Roll(result.DiceResult);
            _camera.Focus(_board.GetPawn(player.Id).GlobalPosition, 10f);
            await _board.AnimatePawn(player.Id, from, result.DiceResult.Total);
            _board.ApplyState(_session.State);
            _busy = false;
            _hud.Render(_session.State, "Landed on " + _session.State.Board.GetTileAt(player.CurrentTileIndex).DisplayName, false);
        }

        private void Buy()
        {
            Execute(new BuyPropertyCommand(_session.State.CurrentPlayerId), "Property purchased.");
        }

        private void Upgrade()
        {
            Execute(new UpgradePropertyCommand(_session.State.CurrentPlayerId), "Property upgraded.");
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
