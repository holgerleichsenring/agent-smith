using AgentSmith.Application.Services.Trace;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.PipelineHarness.Replay;

/// <summary>
/// p0427: the recorded runs committed to this suite, and the answer shapes they were
/// recorded from.
/// </summary>
public static class RecordedRunFixtures
{
    /// <summary>
    /// The shape that ended run 27 at step 12 on 2026-08-16: the analyzer answered
    /// <c>file_count: null</c> for a test project — not a wrong answer, just "I do not
    /// know" — and the naive number read threw straight out of the parse boundary.
    /// </summary>
    public const string AnalyzerMapWithNullFileCount = """
        {
          "primary_language": "csharp",
          "frameworks": ["aspnetcore"],
          "modules": [{"path": "src", "role": "production", "depends_on": []}],
          "test_projects": [{"path": "tests/A", "framework": "xunit", "file_count": null}],
          "entry_points": ["src/Program.cs"],
          "ci": {"has_ci": true, "build_command": "dotnet build", "test_command": "dotnet test"}
        }
        """;

    public const string NullFileCountRun = "analyzer-null-file-count";

    public static string DirectoryOf(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "RecordedRuns", name);

    public static Task<RecordedTrace> LoadAsync(string name) =>
        RecordedTraceFiles.LoadAsync(DirectoryOf(name), CancellationToken.None);

    /// <summary>
    /// How a recorded run BECOMES one of these fixtures: point
    /// <c>AGENTSMITH_EXPORT_RECORDING</c> at a directory under <c>Fixtures/RecordedRuns/</c>
    /// and run the recording test — the same call an operator makes to turn a run that
    /// failed in production into a scenario this suite replays for good.
    /// </summary>
    public const string ExportDirectoryVariable = "AGENTSMITH_EXPORT_RECORDING";

    public static async Task ExportIfRequestedAsync(RecordedTrace trace)
    {
        var directory = Environment.GetEnvironmentVariable(ExportDirectoryVariable);
        if (string.IsNullOrEmpty(directory)) return;
        await RecordedTraceFiles.SaveAsync(trace, directory, CancellationToken.None);
    }
}
