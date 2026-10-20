using System;
using System.Collections.Generic;
using Richman.Core.Events;

namespace Richman.Core
{
    // ADR-0002（Phase 1d-3）：把 GameSession 內「逐格走圖」的移動解析抽成獨立類別。
    //
    // 這是一次純粹的程式碼搬移（mechanical extraction），不是行為重寫：ContinueMovement/
    // MoveOneStep 兩個方法的邏輯逐行照搬自 GameSession，分岔偵測（開啟邊數>1→
    // ChooseRoute）、zero-open-edge 的 MovementBlocked（Phase 1d-2）、pass-through 觸發
    // （目前只有 GrantStartSalary，Phase 1d-2）全部原封不動。事件順序、每個事件的欄位、
    // IDiceRoller/IGameRandom 的呼叫次數與時機完全不變——這些都被黃金雜湊
    // （GoldenReplayTests.cs）與事件序列測試（EventSequenceTests.cs／DeterminismTests.cs）
    // 盯著，任何一點偏差都會讓那些測試失敗。
    //
    // 刻意不直接持有 GameSession：這個類別只需要三樣東西——要走的地圖（BoardDefinition，
    // 唯讀）、要改的狀態（GameState，可變）、把事件登錄進 session 事件序列的管道
    // （Action<GameEvent>，由 GameSession 把自己的 Emit<T> 包成 lambda 傳進來，這樣
    // Sequence/TurnNumber 的蓋章與 _eventLog/_callEvents 的登錄邏輯仍然只有一份）。
    // internal：GameSession 是唯一的生產呼叫端；測試透過 AssemblyInfo.cs 的
    // InternalsVisibleTo 直接建構並驗證。
    internal sealed class MovementResolver
    {
        private readonly BoardDefinition _board;
        private readonly GameState _state;
        private readonly int _startBonus;
        private readonly Action<GameEvent> _emit;

        public MovementResolver(BoardDefinition board, GameState state, int startBonus, Action<GameEvent> emit)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _startBonus = startBonus;
            _emit = emit ?? throw new ArgumentNullException(nameof(emit));
        }

        // 從玩家目前位置開始，逐格把 MovementStepsRemaining 走完，直到下列其中一種情況
        // 發生而提前返回：
        //   - 目前格的開啟邊數為 0——發 MovementBlocked，剩餘步數作廢，玩家停在原地，
        //     回傳 true（見下方回傳值說明）；
        //   - 目前格的開啟邊數 > 1——發 DecisionRequested(ChooseRoute)，暫停等玩家選擇
        //     （GameSession.ExecuteChooseRoute 之後會呼叫 MoveOneStep 把選到的那格走掉，
        //     再呼叫這個方法繼續走剩下的步數），回傳 false。
        // 步數正常走完（MovementStepsRemaining 歸零）則直接跳出 while 迴圈，不發任何
        // 額外事件，回傳 false。
        //
        // 回傳值（Phase 1f review 修正 M3）：true 代表這次呼叫是因為 MovementBlocked 結束
        // 的，呼叫端（GameSession）看到 true 時必須直接進 PostAction（EnterPostAction），
        // 不可以對玩家被卡住的這一格跑 ResolveLanding——被封鎖代表玩家根本沒有「抵達」
        // 這一格（它是移動半路上走不下去的地方，不是終點），不應該觸發稅金／卡片／
        // 決策等落地效果。MovementStepsRemaining==0 這個舊的判斷式沒辦法區分「步數正常
        // 走完」跟「被封鎖提早歸零」，所以需要這個額外的回傳值。
        public bool ContinueMovement(PlayerState player, List<int> path)
        {
            _emit(new PhaseChanged(TurnStage.Move));
            while (_state.MovementStepsRemaining > 0)
            {
                // Phase 1d-2（條件邊）：只考慮「現在開著」的邊——RequiredFlag 未滿足的邊
                // 視為不存在。分岔判斷（是否要停下來等 ChooseRoute）看的是開啟邊數，不是
                // 宣告邊數：同一格在 flag 開/關時，可能一次只有一條路可走、一次有兩條以上。
                var options = _board.GetOpenNextTiles(player.CurrentTileIndex, _state.HasBoardFlag);
                if (options.Count == 0)
                {
                    // 這一格現在一條開著的出邊都沒有：玩家停在原地，本次移動剩餘步數作廢。
                    var stuck = _board.GetTileAt(player.CurrentTileIndex);
                    _emit(new MovementBlocked(player.Id, stuck.PositionIndex, stuck.Id));
                    _state.MovementStepsRemaining = 0;
                    _state.SetRouteChoices(null);
                    return true;
                }
                if (options.Count > 1)
                {
                    _state.SetRouteChoices(options);
                    _state.PendingAction = PendingAction.ChooseRoute;
                    _state.Phase = TurnPhase.AwaitingAction;
                    _state.LastEventMessage = "請選擇接下來的旅遊路線。";
                    var optionIds = new string[options.Count];
                    for (var i = 0; i < options.Count; i++) optionIds[i] = options[i].Id;
                    _emit(new DecisionRequested(DecisionKind.ChooseRoute, player.Id, optionIds, null));
                    return false;
                }
                MoveOneStep(player, options[0], path);
            }
            _state.SetRouteChoices(null);
            return false;
        }

        // 把玩家移動到 destination 這一格（單步），消耗一點 MovementStepsRemaining，
        // 並在 destination 的 PassThroughTrigger 是 GrantStartSalary 時給薪（通用
        // pass-through 觸發，Phase 1d-2；對現有地圖等價於舊的「經過/停在 Start 給薪」）。
        // 公開給 GameSession.ExecuteChooseRoute 直接呼叫——選完岔路後先把那一步走掉，
        // 再呼叫 ContinueMovement 繼續走剩下的步數。
        public void MoveOneStep(PlayerState player, BoardTileDefinition destination, List<int> path)
        {
            var from = player.CurrentTileIndex;
            player.MoveTo(destination.PositionIndex);
            _state.MovementStepsRemaining--;
            path.Add(destination.PositionIndex);
            var passedStart = destination.PassThroughTrigger == PassThroughTriggerKind.GrantStartSalary;
            _emit(new PawnStepped(player.Id, from, destination.PositionIndex, passedStart));
            if (passedStart)
            {
                player.AddMoney(_startBonus);
                // 原規則本來就對 StartBonus==0 的棋盤設定無條件呼叫 AddMoney(0)（行為不變）；
                // 只有金額 > 0 時才發出「領到薪水」事件，避免每次經過起點都產生一筆 0 元雜訊事件。
                if (_startBonus > 0)
                {
                    _emit(new SalaryCollected(player.Id, _startBonus));
                    _emit(new MoneyChanged(player.Id, _startBonus, player.Money, MoneyChangeReason.Salary));
                }
            }
        }
    }
}
