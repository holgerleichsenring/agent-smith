using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>
/// p0324: config-schema fails on parse errors, validation errors, and — the silent
/// classic — a ${SECRET} placeholder that resolved to empty because the env var was
/// never exported.
/// </summary>
public sealed class ConfigSchemaCheckTests
{
    [Fact]
    public async Task RunAsync_LoadFailed_FailsWithParseError()
    {
        var check = new ConfigSchemaCheck(
            FakePreflightConfigSource.LoadFailure("mapping values are not allowed here (line 12)"),
            new AgentSmithConfigValidator());

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("line 12");
        result.FixHint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunAsync_EmptySecret_FailsNamingTheSecret()
    {
        var config = new AgentSmithConfig
        {
            Secrets = new Dictionary<string, string> { ["github_token"] = "", ["ok_secret"] = "value" },
        };
        var check = new ConfigSchemaCheck(
            FakePreflightConfigSource.Of(config), new AgentSmithConfigValidator());

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("github_token").And.NotContain("ok_secret");
        result.FixHint.Should().Contain("environment variable");
    }

    [Fact]
    public async Task RunAsync_ValidConfig_PassesWithCounts()
    {
        var config = new AgentSmithConfig
        {
            Agents = new Dictionary<string, AgentConfig> { ["claude"] = new() },
            Secrets = new Dictionary<string, string> { ["token"] = "resolved" },
        };
        var check = new ConfigSchemaCheck(
            FakePreflightConfigSource.Of(config), new AgentSmithConfigValidator());

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("1 agent(s)");
    }
}
