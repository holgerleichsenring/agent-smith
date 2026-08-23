using System.Text;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0505: writes what the project analyzer emitted for a data repository, verbatim,
/// once per run. Verify resolution consults declared ci commands FIRST — if the
/// analyzer draws a build command for a dbt repository, it wins and a profile's
/// declared list is never reached. The analyzer is an LLM, so one run does not
/// settle the question: a split across the runs is RECORDED as a split, never
/// collapsed to a majority.
/// </summary>
public sealed class AnalyzerCiCommandReport
{
    public string Write(
        IReadOnlyList<ProjectMap> runs, string modelId, string skillsVersion, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "analyzer-ci-commands.md");
        File.WriteAllText(path, Compose(runs, modelId, skillsVersion), Encoding.UTF8);
        return path;
    }

    private static string Compose(
        IReadOnlyList<ProjectMap> runs, string modelId, string skillsVersion)
    {
        var md = new StringBuilder();
        md.AppendLine("# Analyzer ci commands for a dbt repository (p0505)");
        md.AppendLine();
        md.AppendLine($"- model: `{modelId}`");
        md.AppendLine($"- skills pin: `{skillsVersion}`");
        md.AppendLine($"- fixture: `Fixtures/DataFixture/dbt/clean` (copied to a temp dir, InProcessSandbox)");
        md.AppendLine($"- runs: {runs.Count}");
        md.AppendLine();
        md.AppendLine("| run | has_ci | build_command | test_command | ci_system | prerequisites |");
        md.AppendLine("| --- | --- | --- | --- | --- | --- |");
        for (var i = 0; i < runs.Count; i++) md.AppendLine(Row(i + 1, runs[i]));
        md.AppendLine();
        md.AppendLine("Recorded as observed. This file asserts nothing: the eval that writes it "
            + "checks its own mechanics only, because the commit gate runs the suite unfiltered.");
        return md.ToString();
    }

    private static string Row(int index, ProjectMap map) =>
        $"| {index} | {map.Ci.HasCi} | {Cell(map.Ci.BuildCommand)} | {Cell(map.Ci.TestCommand)} "
        + $"| {Cell(map.Ci.CiSystem)} | {Cell(map.Prerequisites)} |";

    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "_(none)_" : $"`{value.Replace("|", "\\|")}`";
}
