using NUnit.Framework;

namespace Richman.Core.Tests
{
    // 架構保護測試（ADR-0003 決策6 / docs/AUDIT.md Blocker B2 / SEC-AUTH-02，Phase 1b）。
    //
    // Richman.Core 必須是零引擎依賴的獨立組件：
    //   1. 不得引用 Godot/GodotSharp 或 UnityEngine（本檔第一個測試）。
    //   2. 下游（Godot/Simulation/Tests）必須透過 ProjectReference 取得它，不能用
    //      <Compile Include> 把原始碼直接吸收進自己的組件——那樣的話 GameSession 的
    //      組件名稱會變成呼叫端自己的組件，internal 邊界形同虛設，而且第一個測試
    //      會變成在檢查呼叫端自己有沒有引用 Godot/Unity，徹底失去意義（本檔第二個測試）。
    public sealed class ArchitectureTests
    {
        [Test]
        public void CoreAssemblyDoesNotReferenceAnyGameEngine()
        {
            var referenced = typeof(GameSession).Assembly.GetReferencedAssemblies();

            foreach (var reference in referenced)
            {
                Assert.That(reference.Name, Does.Not.Contain("Godot"),
                    "Richman.Core must not reference any Godot assembly (found: " + reference.Name + ").");
                Assert.That(reference.Name, Does.Not.Contain("UnityEngine"),
                    "Richman.Core must not reference any Unity assembly (found: " + reference.Name + ").");
            }
        }

        [Test]
        public void GameSessionLivesInADedicatedCoreAssemblyNotAbsorbedIntoTheCallerAssembly()
        {
            var coreAssemblyName = typeof(GameSession).Assembly.GetName().Name;
            var testAssemblyName = typeof(ArchitectureTests).Assembly.GetName().Name;

            Assert.That(coreAssemblyName, Does.StartWith("Richman.Core"),
                "GameSession must live in a dedicated Richman.Core* assembly, not be absorbed via " +
                "<Compile Include> into a caller's assembly (found: " + coreAssemblyName + ").");
            Assert.That(coreAssemblyName, Is.Not.EqualTo(testAssemblyName),
                "GameSession's assembly must differ from this test assembly itself, proving Core was " +
                "consumed via ProjectReference rather than absorbed as source into Richman.Core.Tests.");
        }
    }
}
