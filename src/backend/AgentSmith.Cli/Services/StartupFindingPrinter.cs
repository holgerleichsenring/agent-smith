using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Cli.Services;

/// <summary>
/// p0391b: renders the findings the server would record. One line per finding with its
/// unit and its field — an operator fixing six things needs six lines, each naming the key
/// to edit, not one aggregated wall of text.
/// </summary>
public static class StartupFindingPrinter
{
    public static void Print(IReadOnlyList<StartupFinding> findings, TextWriter writer)
    {
        if (findings.Count == 0)
        {
            writer.WriteLine("Configuration is valid — no findings.");
            return;
        }

        foreach (var finding in findings)
            writer.WriteLine($"{Severity(finding)} {Unit(finding)}: {finding.Reason}");

        var blocking = findings.Count(f => f.IsBlocking);
        writer.WriteLine(
            $"{findings.Count} finding(s), {blocking} blocking. "
            + "The server would start and report these; a run on a blocked unit would not.");
    }

    private static string Severity(StartupFinding finding) => finding.IsBlocking ? "[blocking]" : "[advisory]";

    private static string Unit(StartupFinding finding) => string.Join(
        " / ",
        new[] { finding.Subsystem, finding.Project, finding.Trigger, finding.Field }
            .Where(part => !string.IsNullOrEmpty(part)));
}
