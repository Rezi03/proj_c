using System.Diagnostics;
using System.Runtime.InteropServices;
using Quant.Data;
using VolForecast.Models;
using VolForecast.Report;

namespace VolForecast;

public static class Program
{
    public static async Task<int> Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var sw = Stopwatch.StartNew();
        Console.WriteLine();
        Console.WriteLine("  ═══════════════════════════════════════════════════");
        Console.WriteLine("   PRÉVISION DE LA VOLATILITÉ DU CAC 40  ·  C# + C++");
        Console.WriteLine("  ═══════════════════════════════════════════════════");

        try
        {
            Console.WriteLine("\n[1/4] Téléchargement des cours (historique + séance du jour)");
            var bars = await MarketData.LoadAsync(Config.Symbol, Config.HistoryStart, "data");

            Console.WriteLine($"\n[2/4] Estimation et prévisions hors échantillon  ·  moteur GARCH : {Garch.EngineLabel}");
            var study = Forecaster.Run(bars);
            foreach (var m in study.Models.OrderBy(m => m.Score!.Qlike))
                Console.WriteLine($"  {m.Name,-28} QLIKE = {m.Score!.Qlike:0.0000}   demain : {Math.Sqrt(252 * m.NextDay):P1}");
            Console.WriteLine($"  {study.Refits} estimations en {study.FitMilliseconds / 1000:0.0} s");

            Console.WriteLine("\n[3/4] Génération du rapport PDF");
            var path = VolReport.Build(study, "output");
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
