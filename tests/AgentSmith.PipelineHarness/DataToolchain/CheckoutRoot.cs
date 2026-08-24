namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0505: resolves the checkout root by walking up from the test's base directory
/// to the folder holding AgentSmith.sln — the same move ExpectationGoldenEvalTests
/// makes for its report path. Fixtures and reports must be read from the SOURCE
/// tree: the harness re-copies Fixtures/** into bin on every build, so a bin-dir
/// read would hash a stale copy and a bin-dir write would never be committed.
/// </summary>
public sealed class CheckoutRoot
{
    public string Resolve()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgentSmith.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException(
                $"no AgentSmith.sln above {AppContext.BaseDirectory} — run from a checkout");
    }

    public string FixturesDirectory() => Path.Combine(
        Resolve(), "tests", "AgentSmith.PipelineHarness", "Fixtures", "DataFixture");

    public string ReportsDirectory() => Path.Combine(
        Resolve(), "tests", "AgentSmith.PipelineHarness", "Reports", "data-toolchain");
}
