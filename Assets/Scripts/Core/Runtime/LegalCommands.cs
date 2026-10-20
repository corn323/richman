using System;
using System.Collections.Generic;

namespace Richman.Core
{
    // ADR-0003 決策7：GetLegalCommands(playerId) 的回傳型別。
    //
    // 設計刻意保持最小：命令種類 + 必要參數的 id 清單。足夠讓呼叫端（Bot/HUD）知道
    // 「現在可以送出哪些命令、以及每種命令可選的參數」，不需要各自重新判斷
    // Phase/PendingAction/money/UpgradeLevel 等細節——那些判斷現在集中在
    // GameState.GetLegalCommands 一處。
    public enum LegalCommandKind
    {
        RollDice,
        BuyProperty,
        UpgradeProperty,
        ChooseRoute,
        EndTurn,
        Trade
    }

    public sealed class LegalCommandOption
    {
        private static readonly IReadOnlyList<string> NoParameters = Array.Empty<string>();

        public LegalCommandOption(LegalCommandKind kind, IReadOnlyList<string> parameterIds = null)
        {
            Kind = kind;
            ParameterIds = parameterIds ?? NoParameters;
        }

        public LegalCommandKind Kind { get; }

        /// <summary>
        /// For <see cref="LegalCommandKind.ChooseRoute"/>: the connected next-tile ids the player
        /// may choose between (same order as <see cref="GameState.RouteChoiceTileIds"/>).
        /// For <see cref="LegalCommandKind.UpgradeProperty"/>: the property ids the player
        /// currently owns, is not yet at max level, and can afford to upgrade right now.
        /// Empty for command kinds that take no parameter.
        /// </summary>
        public IReadOnlyList<string> ParameterIds { get; }
    }
}
