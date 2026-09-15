using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Richman.Editor
{
    public static class BuildProject
    {
        private const string ScenePath = "Assets/Scenes/Playtest.unity";
        private const string OutputPath = "Builds/Windows/Richman.exe";

        public static void BuildWindows()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("Windows build failed: " + report.summary.result);
            }

            Debug.Log("Windows build succeeded: " + Path.GetFullPath(OutputPath));
        }
    }
}
