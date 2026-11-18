using System.Diagnostics;
using System.Runtime.InteropServices;
using Quant.Data;
using RiskEngine.Analytics;
using RiskEngine.Report;

namespace RiskEngine;

public static class Program
{
    public static async Task<int> Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var sw = Stopwatch.StartNew();
        Console.WriteLine();
        Console.WriteLine("  ═══════════════════════════════════════════════");
        Console.WriteLine("   MOTEUR DE RISQUE DE MARCHÉ  ·  C# .NET + C++");
        Console.WriteLine("  ═══════════════════════════════════════════════");

        try
        {
            Console.WriteLine("\n[1/4] Téléchargement des cours (historique + séance du jour)");
            var raw = new Dictionary<string, List<Bar>>();
            foreach (var a in Config.Assets)
                raw[a.Ticker] = await MarketData.LoadAsync(a.Ticker, Config.HistoryStart, "data");
            raw[Config.Benchmark] = await MarketData.LoadAsync(Config.Benchmark, Config.HistoryStart, "data");

            Console.WriteLine($"\n[2/4] Calculs  ·  moteur Monte Carlo : {MonteCarlo.EngineLabel}");
            var result = RiskAnalysis.Run(raw);
            foreach (var m in result.Methods)
                Console.WriteLine($"  {m.Name,-24} VaR 99 % = {m.Var,12:N0} €   ES 97,5 % = {m.Es,12:N0} €");
            Console.WriteLine($"  Backtesting 250 j : {result.BtHist.Exceptions.Count} exceptions, zone {result.BtHist.Zone.ToLowerInvariant()}");

            Console.WriteLine("\n[3/4] Génération du rapport PDF");
            var path = RiskReport.Build(result, "output");
            Console.WriteLine($"  ✓ {Path.GetFullPath(path)}");

            Console.WriteLine($"\n[4/4] Terminé en {sw.Elapsed.TotalSeconds:0.0} s");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start(new ProcessStartInfo("open", $"\"{path}\"") { UseShellExecute = false });
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nErreur : {ex.Message}");
            Console.ResetColor();
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
