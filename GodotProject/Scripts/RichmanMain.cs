using Godot;
using Richman.Core;
using System.Collections.Generic;

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
        private int _sessionSeed;
        private readonly int[] _characters = { 0, 1, 2, 3 };
        private readonly Color[] _colors = { new Color("#bf705f"), new Color("#6387a5"), new Color("#82956f"), new Color("#a080a0") };

        // 純顯示用的固定 seed：開局前的角色預覽棋盤不是真正的對局，不需要、也不應該
        // 消耗/影響正式對局的亂數序列（IRng 強制注入後，建構 GameSession 一定要給一個
        // seed，這裡刻意寫死，和下方 StartGame() 的真實對局 seed 無關）。
        private const int PreviewOnlySeed = 928371;

        public override void _Ready()
        {
            BuildPresentationBootstrap();

            _board = new GodotBoardView { Name = "Board" };
            AddChild(_board);
            var board = PrototypeBoardFactory.Create();
            var setup = new List<PlayerSetup> { new PlayerSetup(1, "旅人・小徑"), new PlayerSetup(2, "建築師・石榴"), new PlayerSetup(3, "園藝師・苔苔"), new PlayerSetup(4, "觀星者・弦月") };
            var previewSession = new GameSession(GameModeConfig.ClassicBankruptcyFourPlayer(), board, setup, new SeededDiceRoller(PreviewOnlySeed), new SeededRandom(PreviewOnlySeed));
            _board.Build(board, previewSession.State.Players, _characters, _colors);
            _dice = new GodotDiceView { Name = "Dice" };
            AddChild(_dice);
            _dice.Build();
            _camera = new GodotCameraRig { Name = "CameraRig" };
            AddChild(_camera);
            _camera.FocusOverview();
            _hud = new GodotGameplayHud { Name = "GameplayHUD" };
            AddChild(_hud);
            _hud.Build(StartGame, PreviewCharacter, Roll, Buy, Upgrade, EndTurn, ChooseRoute, Trade);
            if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--test-pawn-move") >= 0)
                _ = RunPawnMoveSmoke();
        }

        private void PreviewCharacter(int playerIndex, int character, Color color)
        {
            _characters[playerIndex] = character; _colors[playerIndex] = color;
            _board.SetPreview(playerIndex + 1, character, color);
        }

        private void StartGame()
        {
            _characters[0] = _hud.SelectedCharacters[0]; _characters[1] = _hud.SelectedCharacters[1];
            _characters[2] = _hud.SelectedCharacters[2]; _characters[3] = _hud.SelectedCharacters[3];
            _colors[0] = _hud.SelectedColors[0]; _colors[1] = _hud.SelectedColors[1];
            _colors[2] = _hud.SelectedColors[2]; _colors[3] = _hud.SelectedColors[3];
            var names = new[] { "旅人・小徑", "建築師・石榴", "園藝師・苔苔", "觀星者・弦月" };
            var players = new List<PlayerSetup>();
            for (var i = 0; i < 4; i++) players.Add(new PlayerSetup(i + 1, names[_characters[i]]));
            // ADR-0003 決策5 / docs/AUDIT.md Blocker B1：composition root 在「每次開局」產生一次
            // 本局 seed（而不是寫死常數，也不是整個 App 生命週期共用一個），並印出來以便重播。
            // 骰子與卡牌/法院亂數從同一個 seed 固定衍生出兩個不同的子 seed，
            // 讓兩個亂數流互相獨立，不會因為共用同一個生成器狀態而產生意外相關性。
            _sessionSeed = (int)GD.Randi();
            GD.Print("SESSION_SEED=" + _sessionSeed);
            var diceSeed = unchecked(_sessionSeed ^ 0x5DEECE);
            _session = new GameSession(GameModeConfig.ClassicBankruptcyFourPlayer(), PrototypeBoardFactory.Create(), players,
                new SeededDiceRoller(diceSeed), new SeededRandom(_sessionSeed));
            _board.SetPreview(1, _characters[0], _colors[0]); _board.SetPreview(2, _characters[1], _colors[1]);
            _board.SetPreview(3, _characters[2], _colors[2]); _board.SetPreview(4, _characters[3], _colors[3]);
            _board.SyncPawns(_session.State); _board.ApplyState(_session.State);
            _hud.ShowGame(); _hud.Render(_session.State, "旅程開始！輪到 " + players[0].DisplayName + "。", false);
        }

        private async System.Threading.Tasks.Task RunPawnMoveSmoke()
        {
            try
            {
                StartGame();
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
                    BackgroundColor = new Color("#d8d3c8"),
                    AmbientLightSource = Environment.AmbientSource.Color,
                    AmbientLightColor = new Color("#ded8cc"),
                    AmbientLightEnergy = 0.82f,
                    TonemapMode = Environment.ToneMapper.Filmic
                }
            };
            AddChild(environment);

            var light = new DirectionalLight3D
            {
                RotationDegrees = new Vector3(-52f, -28f, 0f),
                LightColor = new Color("#f4e9d4"),
                LightEnergy = 0.92f,
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

        private void Trade(TradeCommand command)
        {
            if (_busy || _session.State.Outcome != null) return;
            var result = _session.Execute(command);
            _board.ApplyState(_session.State);
            _hud.Render(_session.State, result.Succeeded ? _session.State.LastEventMessage : result.ErrorMessage, false);
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
