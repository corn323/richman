using System;
using System.Collections.Generic;

namespace Richman.Core
{
    public sealed class SimulationGameResult
    {
        internal SimulationGameResult(int seed, int turns, int winnerId, bool completed)
        { Seed = seed; Turns = turns; WinnerId = winnerId; Completed = completed; }
        public int Seed { get; }
        public int Turns { get; }
        public int WinnerId { get; }
        public bool Completed { get; }
    }

    public sealed class SimulationReport
    {
        private readonly List<SimulationGameResult> _games;
        internal SimulationReport(List<SimulationGameResult> games, int maxTurns) { _games = games; MaxTurns = maxTurns; }
        public IReadOnlyList<SimulationGameResult> Games => _games.AsReadOnly();
        public int MaxTurns { get; }
        public int CompletedGames
        {
            get { var count = 0; for (var i = 0; i < _games.Count; i++) if (_games[i].Completed) count++; return count; }
        }
        public int AverageTurns
        {
            get { if (_games.Count == 0) return 0; long total = 0; for (var i = 0; i < _games.Count; i++) total += _games[i].Turns; return (int)(total / _games.Count); }
        }
        public string ToCsv()
        {
            var text = "seed,turns,winnerId,completed\n";
            for (var i = 0; i < _games.Count; i++)
            {
                var game = _games[i];
                text += game.Seed + "," + game.Turns + "," + game.WinnerId + "," + game.Completed + "\n";
            }
            return text;
        }
    }

    public static class BotSimulation
    {
        public static SimulationReport Run(int gameCount, int firstSeed, int maxCommandsPerGame, IBotStrategy strategy = null)
        {
            if (gameCount < 1) throw new ArgumentOutOfRangeException(nameof(gameCount));
            if (maxCommandsPerGame < 1) throw new ArgumentOutOfRangeException(nameof(maxCommandsPerGame));
            strategy = strategy ?? new SimpleBotStrategy();
            var games = new List<SimulationGameResult>(gameCount);
            for (var gameIndex = 0; gameIndex < gameCount; gameIndex++)
            {
                var seed = unchecked(firstSeed + gameIndex);
                var session = PrototypeGameFactory.CreateFourPlayerSession(seed);
                var commands = 0;
                while (session.State.Phase != TurnPhase.Finished && commands < maxCommandsPerGame)
                {
                    var playerId = session.State.CurrentPlayerId;
                    var result = session.Execute(strategy.ChooseCommand(session.State, playerId));
                    if (!result.Succeeded) throw new InvalidOperationException("Bot produced a rejected command: " + result.ErrorCode);
                    commands++;
                }
                games.Add(new SimulationGameResult(seed, session.State.TurnNumber,
                    session.State.Outcome == null ? 0 : session.State.Outcome.WinnerId,
                    session.State.Phase == TurnPhase.Finished));
            }
            return new SimulationReport(games, maxCommandsPerGame);
        }
    }
}
