using AgentSmith.PipelineHarness.Presets;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199e: per-preset docker-tier fixture layout. Most presets use the
/// csharp fixture + the default docker yml; legal-analysis needs its own
/// fixture (python toolchain + pip install markitdown) plus a real inbox
/// document path for AcquireSource to copy into /work.
/// </summary>
internal sealed record DockerPresetLayout(
    string ConfigYml, string FixtureSourceDir, string? SourceFilePath = null)
{
    public static DockerPresetLayout For(string preset) =>
        string.Equals(preset, "legal-analysis", StringComparison.OrdinalIgnoreCase)
            ? new DockerPresetLayout(
                FixturePaths.DockerLegal,
                FixturePaths.LegalFixtureSource(),
                Path.Combine(FixturePaths.LegalFixtureSource(), "inbox", "sample.txt"))
            : new DockerPresetLayout(
                FixturePaths.Docker,
                FixturePaths.CsharpFixtureSource(),
                SourceFilePath: null);
}
