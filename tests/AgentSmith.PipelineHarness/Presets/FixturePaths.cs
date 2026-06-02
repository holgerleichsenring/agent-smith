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
    public const string Docker = "agentsmith-docker.yml";
    public const string DockerNoRegistries = "agentsmith-docker-no-registries.yml";

    /// <summary>
    /// p0199b: fixture C# project ships as content next to the harness
    /// dll; resolves to the directory that DockerHarnessSession seeds
    /// into a fresh working copy + bare repo for each docker-tier test.
    /// </summary>
    public static string CsharpFixtureSource() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "CsharpFixture");
}
