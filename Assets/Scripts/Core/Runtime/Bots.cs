using System;

namespace Richman.Core
{
    public interface IBotStrategy
    {
        GameCommand ChooseCommand(GameState state, int playerId);
    }

    // ADR-0003 決策7：改用 GameState.GetLegalCommands 取得「現在能做什麼」，取代原本
    // 自行重算 Phase/PendingAction/money/UpgradeLevel 的判斷（docs/AUDIT.md Major：
    // 三處重複之一，另兩處為 GodotGameplayHud.cs/GameplayHUD.cs，留待 Phase 4 處理）。
    //
    // 刻意保留依 Phase 分支的外層結構，且 AwaitingEndTurn 分支刻意不理會
    // GetLegalCommands 可能回報的「起點格任意自有地產」升級選項（CanUpgradeOwnedProperty），
    // 永遠直接結束回合——這是為了讓這個最簡單 Bot 的行為與 Phase 1a 黃金重播雜湊
    // （GoldenReplayTests.cs）完全一致，不產生額外命令、不消耗額外亂數。
    public sealed class SimpleBotStrategy : IBotStrategy
    {
        public GameCommand ChooseCommand(GameState state, int playerId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var player = state.GetPlayer(playerId);
            if (player == null) throw new ArgumentException("The bot player is not in the game.", nameof(playerId));
            if (state.CurrentPlayerId != playerId) throw new InvalidOperationException("A bot can only choose for the current player.");

            switch (state.Phase)
            {
                case TurnPhase.AwaitingRoll:
                    return new RollDiceCommand(playerId);

                case TurnPhase.AwaitingAction:
                {
                    var legalCommands = state.GetLegalCommands(playerId);
                    for (var i = 0; i < legalCommands.Count; i++)
                    {
                        if (legalCommands[i].Kind == LegalCommandKind.ChooseRoute && legalCommands[i].ParameterIds.Count > 0)
                            return new ChooseRouteCommand(playerId, legalCommands[i].ParameterIds[0]);
                    }
                    for (var i = 0; i < legalCommands.Count; i++)
                    {
                        if (legalCommands[i].Kind == LegalCommandKind.BuyProperty)
                            return new BuyPropertyCommand(playerId);
                    }
                    for (var i = 0; i < legalCommands.Count; i++)
                    {
                        if (legalCommands[i].Kind == LegalCommandKind.UpgradeProperty)
                            return new UpgradePropertyCommand(playerId);
                    }
                    return new EndTurnCommand(playerId);
                }

                case TurnPhase.AwaitingEndTurn:
                    return new EndTurnCommand(playerId);

                default:
                    throw new InvalidOperationException("The game no longer accepts bot commands.");
            }
        }
    }
}
