namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199: resolves harness fixture YAML paths relative to the test
/// assembly output. The harness copies <c>Fixtures/</c> alongside the
/// built dll (see csproj <c>CopyToOutputDirectory</c>); every preset
/// test reads its agentsmith.yml through this helper.
/// </summary>
internal static class FixturePaths
{
    public static string For(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", filename);

    public const string Default = "agentsmith.yml";
    public const string NoRegistries = "agentsmith-no-registries.yml";
}
