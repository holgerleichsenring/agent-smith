using AgentSmith.PipelineHarness.Presets;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// Per-preset docker-tier fixture layout. Most presets reuse the csharp
/// fixture + agentsmith-docker.yml. legal-analysis (p0199e) ships its own
/// python+markitdown layout. api-security-scan (p0199f) carries two layouts:
/// the default passive-mode wires the StubApiTargetHost Kestrel server (no
/// source), and the opt-in source-mode reuses the csharp fixture so
/// TryCheckoutSource's CLI-override branch publishes Repository.
/// </summary>
internal sealed record DockerPresetLayout(
    string ConfigYml,
    string FixtureSourceDir,
    DockerPresetSourceMode SourceMode,
    string? SourceFilePath = null)
{
    public static DockerPresetLayout For(string preset) =>
        string.Equals(preset, "legal-analysis", StringComparison.OrdinalIgnoreCase)
            ? Legal()
            : string.Equals(preset, "api-security-scan", StringComparison.OrdinalIgnoreCase)
                ? ApiPassive()
                : DefaultCsharp();

    public static DockerPresetLayout ApiSourceMode() => new(
        FixturePaths.Docker, FixturePaths.CsharpFixtureSource(),
        DockerPresetSourceMode.Source);

    private static DockerPresetLayout Legal() => new(
        FixturePaths.DockerLegal, FixturePaths.LegalFixtureSource(),
        DockerPresetSourceMode.Source,
        Path.Combine(FixturePaths.LegalFixtureSource(), "inbox", "sample.txt"));

    private static DockerPresetLayout ApiPassive() => new(
        FixturePaths.DockerApiPassive, FixturePaths.CsharpFixtureSource(),
        DockerPresetSourceMode.Passive);

    private static DockerPresetLayout DefaultCsharp() => new(
        FixturePaths.Docker, FixturePaths.CsharpFixtureSource(),
        DockerPresetSourceMode.Source);
}
