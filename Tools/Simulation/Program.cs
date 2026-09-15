using System;
using System.IO;
using Richman.Core;

namespace Richman.Simulation
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var gameCount = args.Length > 0 ? int.Parse(args[0]) : 1000;
            var report = BotSimulation.Run(gameCount, 928371, 20000);
            var outputPath = "SimulationResults.csv";
            File.WriteAllText(outputPath, report.ToCsv());
            Console.WriteLine("Games: " + report.Games.Count);
            Console.WriteLine("Completed: " + report.CompletedGames);
            Console.WriteLine("Average turns: " + report.AverageTurns);
            Console.WriteLine("CSV: " + Path.GetFullPath(outputPath));
            return report.CompletedGames == report.Games.Count ? 0 : 2;
        }
    }
}
