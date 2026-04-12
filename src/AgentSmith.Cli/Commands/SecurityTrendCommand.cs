using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using AgentSmith.Application.Services.Handlers;

namespace AgentSmith.Cli.Commands;

internal static class SecurityTrendCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var projectOption = new Option<string>("--project", "Project directory to read security snapshots from") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Show what would be analyzed without executing");

        var cmd = new Command("security-trend", "Show security scan trend analysis")
        {
            projectOption, configOption, verboseOption, dryRunOption
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption)!;
            var isDryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
            var projectPath = Path.GetFullPath(project);
            var securityDir = Path.Combine(projectPath, ".agentsmith", "security");

            if (isDryRun)
            {
                Console.WriteLine("Dry run - would execute:");
                Console.WriteLine($"  Command:  security-trend");
                Console.WriteLine($"  Project:  {projectPath}");
                Console.WriteLine("  Steps:");
                Console.WriteLine("    - Load security snapshots from .agentsmith/security/");
                Console.WriteLine("    - Compute trend analysis (critical/high history, costs)");
                Console.WriteLine("    - Print trend report to console");
                return;
            }

            if (!Directory.Exists(securityDir))
            {
                Console.Error.WriteLine($"No security snapshots found in {securityDir}");
                ctx.ExitCode = 1;
                return;
            }

            var snapshots = SecurityTrendHandler.LoadSnapshots(securityDir);

            if (snapshots.Count == 0)
            {
                Console.Error.WriteLine("No valid security snapshots found");
                ctx.ExitCode = 1;
                return;
            }

            var ordered = snapshots.OrderByDescending(s => s.Date).ToList();
            var last = ordered[0];
            var best = ordered.MinBy(s => s.FindingsRetained)!;
            var worst = ordered.MaxBy(s => s.FindingsRetained)!;

            var criticalHistory = string.Join(" \u2192 ", ordered.AsEnumerable().Reverse().Select(s => s.FindingsCritical.ToString(CultureInfo.InvariantCulture)));
            var highHistory = string.Join(" \u2192 ", ordered.AsEnumerable().Reverse().Select(s => s.FindingsHigh.ToString(CultureInfo.InvariantCulture)));

            var totalCost = ordered.Sum(s => s.CostUsd);
            var avgCost = totalCost / ordered.Count;

            var projectName = Path.GetFileName(projectPath);

            Console.WriteLine($"Security Trend for {projectName} ({ordered.Count} scans)");
            Console.WriteLine("\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550");
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Last scan:         {last.Date:yyyy-MM-dd} (${last.CostUsd:F2}, {last.FindingsRetained} retained)"));
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Best scan:         {best.Date:yyyy-MM-dd} ({best.FindingsRetained} retained)"));
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Worst scan:        {worst.Date:yyyy-MM-dd} ({worst.FindingsRetained} retained)"));
            Console.WriteLine();
            Console.WriteLine($"Critical history:  {criticalHistory}");
            Console.WriteLine($"High history:      {highHistory}");
            Console.WriteLine();
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Avg cost/scan:     ${avgCost:F2}"));
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Total invested:    ${totalCost:F2}"));
        });

        return cmd;
    }
}
