using System.Diagnostics;
using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

/// <summary>
/// 2026-11-25-9992d: every toolchain container carries every repository's scoped secrets; a command gets its own
/// repository's under their plain names and none of another's.
/// </summary>
public sealed class ScopedSecretEnvironmentTests
{
    private static ProcessStartInfo Container()
    {
        var info = new ProcessStartInfo("sh");
        info.Environment["FEED_TOKEN"] = "project-wide";
        info.Environment[ScopedSecretNames.For("api", "DB_PASSWORD")] = "api-db";
        info.Environment[ScopedSecretNames.For("api", "API_KEY")] = "api-key";
        info.Environment[ScopedSecretNames.For("web", "DB_PASSWORD")] = "web-db";
        info.Environment[ScopedSecretNames.For("web", "NPM_TOKEN")] = "web-npm";
        return info;
    }

    private static IEnumerable<string> Scoped(ProcessStartInfo info) =>
        info.Environment.Keys.Where(ScopedSecretNames.IsScoped);

    [Fact]
    public void Scrub_AStepScopedToA_GetsAsVariablesUnderTheirNames_NotBs()
    {
        var info = Container();

        AgentProcessEnvironment.Scrub(info, null, "api");

        info.Environment["DB_PASSWORD"].Should().Be("api-db");
        info.Environment["API_KEY"].Should().Be("api-key");
        info.Environment.Keys.Should().NotContain("NPM_TOKEN");
        info.Environment.Values.Should().NotContain(["web-db", "web-npm"]);
        Scoped(info).Should().BeEmpty("no scoped name ever reaches a command, its own repository's included");
    }

    [Fact]
    public void Scrub_NoScope_LeavesNoScopedVariableAtAll()
    {
        var info = Container();

        AgentProcessEnvironment.Scrub(info, null);

        Scoped(info).Should().BeEmpty();
        info.Environment.Keys.Should().NotContain(["DB_PASSWORD", "API_KEY", "NPM_TOKEN"]);
    }

    [Fact]
    public void Scrub_AStepsOwnEnv_WinsOverItsRepositorysSecret()
    {
        var info = Container();
        var stepEnv = new Dictionary<string, string> { ["DB_PASSWORD"] = "from-the-step" };
        info.Environment["DB_PASSWORD"] = "from-the-step";

        AgentProcessEnvironment.Scrub(info, stepEnv, "api");

        info.Environment["DB_PASSWORD"].Should().Be("from-the-step");
        info.Environment["API_KEY"].Should().Be("api-key");
    }

    [Fact]
    public void Scrub_ProjectWideVariables_AreUntouchedWhateverTheScope()
    {
        foreach (var scope in new[] { null, "api", "web", "unknown" })
        {
            var info = Container();

            AgentProcessEnvironment.Scrub(info, null, scope);

            info.Environment["FEED_TOKEN"].Should().Be("project-wide");
        }
    }

    [Fact]
    public void Scrub_AScopeNoRepositoryCarries_AddsNothing()
    {
        var info = Container();

        AgentProcessEnvironment.Scrub(info, null, "unknown");

        info.Environment.Keys.Should().NotContain(["DB_PASSWORD", "API_KEY", "NPM_TOKEN"]);
        Scoped(info).Should().BeEmpty();
    }
}
