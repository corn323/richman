using System.Runtime.CompilerServices;

// Phase 1d-2：GameSession.SetBoardFlag/ClearBoardFlag（以及其他目前沒有 Command 入口的
// internal 成員）刻意維持 internal——它們的設計呼叫端是 Core 內部的未來 Effect 系統
// （Phase 3），不是外部呼叫端。這裡只讓 Richman.Core.Tests 這個「同一個 repo、同一份
// 職責邊界」的測試組件可以直接呼叫 internal 成員驗證行為，不影響 ArchitectureTests.cs
// 鎖住的「Core 必須是獨立組件、透過 ProjectReference 消費」——internal 仍然對 Godot/
// Simulation 等其他下游組件不可見。
[assembly: InternalsVisibleTo("Richman.Core.Tests")]
