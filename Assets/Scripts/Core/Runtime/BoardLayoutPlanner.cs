using System;
using System.Collections.Generic;

namespace Richman.Core
{
    // ADR-0002 §3（Phase 1d-3）：視覺佈局計算移出 BoardDefinition 的建構流程，改成一個
    // 獨立、顯式呼叫的 static helper。
    //
    // 展示用，規則路徑禁止讀取 MapPosition：BoardTileDefinition.MapPosition 只是給
    // Presentation（GodotBoardView）與匯出工具（Tools/Simulation --export-map）畫面/座標
    // 用的「佈局提示」，不影響任何規則判斷——Core 本身（GameSession、MovementResolver、
    // BoardDefinition 的圖驗證）完全不讀取它，也不應該讀取它。沒有任何呼叫端呼叫
    // AssignMapPositions 時，所有 tile 的 MapPosition 維持 BoardMapPosition 的預設值
    // （全 0），遊戲規則依然完全可運作——移動、分岔、租金、破產等判斷全部只看
    // NextTileIds/Edges 這張圖與 GameState，不看座標。
    //
    // 沿用舊 BoardDefinition.AssignMapPositions（Phase 1d-1 之前內建在建構子裡）的假設：
    // 前 MainRouteTileCount 格依陣列順序構成一個橢圓主環；非主環的分支格必須沿著單一
    // 出口鏈接回某個主環格，否則丟 ArgumentException。這不是圖結構規則（BoardDefinition
    // 建構子不會因為佈局算不出來而拒絕合法的一般圖），純粹是這個橢圓＋Bezier 佈局演算法
    // 自己對輸入形狀的假設——呼叫端（Presentation/匯出工具）如果用了不符合這個假設的
    // 一般圖（多出口分支等 Phase 1d-1/1d-2 之後才合法的形狀），需要自己決定要不要
    // try/catch，或者改用其他佈局演算法。
    public static class BoardLayoutPlanner
    {
        public static void AssignMapPositions(BoardDefinition board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            var tiles = board.Tiles;
            var mainRouteTileCount = board.MainRouteTileCount;

            for (var i = 0; i < mainRouteTileCount; i++)
            {
                var angle = (Math.PI * 2.0 * i / mainRouteTileCount) - (Math.PI / 2.0);
                var x = (float)(11.7 * Math.Cos(angle) + 0.42 * Math.Sin(3.0 * angle));
                var z = (float)(8.3 * Math.Sin(angle) + 0.30 * Math.Sin(2.0 * angle));
                var y = (float)(0.62 + 0.16 * Math.Sin(i * 0.71) + 0.07 * Math.Sin(i * 1.89));
                tiles[i].SetMapPosition(new BoardMapPosition(x, y, z, 0f));
            }

            for (var sourceIndex = 0; sourceIndex < mainRouteTileCount; sourceIndex++)
            {
                var source = tiles[sourceIndex];
                for (var edge = 0; edge < source.NextTileIds.Count; edge++)
                {
                    var branchId = source.NextTileIds[edge];
                    var branch = board.GetTile(branchId);
                    if (branch.IsMainRoute) continue;
                    var branchNodes = new List<BoardTileDefinition>();
                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    var current = branch;
                    while (!current.IsMainRoute)
                    {
                        if (!visited.Add(current.Id)) throw new ArgumentException("Branch contains a cycle before merging: " + current.Id);
                        branchNodes.Add(current);
                        if (current.NextTileIds.Count != 1) throw new ArgumentException("Each branch waypoint must have one onward route: " + current.Id);
                        current = board.GetTile(current.NextTileIds[0]);
                    }
                    var end = current.MapPosition;
                    var start = source.MapPosition;
                    var midX = (start.X + end.X) * 0.5f;
                    var midZ = (start.Z + end.Z) * 0.5f;
                    var length = MathF.Sqrt(midX * midX + midZ * midZ);
                    var controlX = midX - (length > 0.001f ? midX / length * 2.5f : 0f);
                    var controlZ = midZ - (length > 0.001f ? midZ / length * 2.5f : 0f);
                    for (var waypoint = 0; waypoint < branchNodes.Count; waypoint++)
                    {
                        var t = (waypoint + 1f) / (branchNodes.Count + 1f);
                        var inverse = 1f - t;
                        var x = inverse * inverse * start.X + 2f * inverse * t * controlX + t * t * end.X;
                        var z = inverse * inverse * start.Z + 2f * inverse * t * controlZ + t * t * end.Z;
                        branchNodes[waypoint].SetMapPosition(new BoardMapPosition(x, 0.85f, z, 0f));
                    }
                }
            }

            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var next = board.GetTile(tile.NextTileIds[0]).MapPosition;
                var position = tile.MapPosition;
                var rotation = (float)(-Math.Atan2(next.Z - position.Z, next.X - position.X) * 180.0 / Math.PI);
                tile.SetMapPosition(new BoardMapPosition(position.X, position.Y, position.Z, rotation));
            }
        }
    }
}
