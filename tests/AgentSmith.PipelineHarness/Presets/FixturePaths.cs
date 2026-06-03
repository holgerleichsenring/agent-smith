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
    public const string DockerLegal = "agentsmith-docker-legal.yml";
    public const string DockerApiPassive = "agentsmith-docker-api-passive.yml";

    /// <summary>
    /// p0199b: fixture C# project ships as content next to the harness
    /// dll; resolves to the directory that DockerHarnessSession seeds
    /// into a fresh working copy + bare repo for each docker-tier test.
    /// </summary>
    public static string CsharpFixtureSource() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "CsharpFixture");

    /// <summary>
    /// p0199e: legal-analysis docker-tier source dir. Carries only a
    /// .agentsmith/contexts/default/context.yaml (python + pip install
    /// markitdown) plus a small inbox/ sample. The repo content itself
    /// is irrelevant for legal-analysis — AcquireSource ignores it and
    /// pushes the SourceFilePath document into /work directly.
    /// </summary>
    public static string LegalFixtureSource() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegalFixture");

    /// <summary>
    /// p0199e: parallel legal source dir whose context.yaml intentionally
    /// drops ci.install_command. Drives the negative test that proves
    /// InstallDependencies is the gate for BootstrapDocument's markitdown.
    /// </summary>
    public static string LegalFixtureNoInstallSource() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegalFixtureNoInstall");

    /// <summary>
    /// p0199f: minimal OpenAPI spec served by StubApiTargetHost. The
    /// passive-mode api-security-scan fixture points SwaggerPath / ApiTarget
    /// at this file (over the Kestrel mini-server) so LoadSwagger parses a
    /// real spec and downstream scanner stubs run against a real-looking
    /// target URL.
    /// </summary>
    public static string StubApiTargetOpenApi() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "StubApiTarget", "openapi.json");
}
