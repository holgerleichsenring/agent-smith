using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// p0428: a feed credential that resolved to nothing is named by HOST — never by value —
/// instead of surfacing an hour later as a 401 inside a build log. It REPORTS: whether
/// the host is one this run actually restores from is not knowable here, and the harness
/// showed a gate refusing 14 healthy runs over a registry none of them used.
/// </summary>
public sealed class RegistryCredentialCheckTests
{
    [Fact]
    public async Task ARegistryWithoutAToken_IsNamedWithoutItsValue()
    {
        var config = new AgentSmithConfig
        {
            Registries =
            [
                new RegistryConfig("feed.example.com", "any", string.Empty),
                new RegistryConfig("other.example.com", "any", "a-real-secret"),
            ],
        };

        var finding = await new RegistryCredentialCheck(config)
            .RunAsync(new PipelineContext(), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Warn,
            "an unresolved token only matters if a repo names that host — a refusal here "
            + "would stop runs that never touch the feed");
        finding.Message.Should().Contain("feed.example.com");
        finding.Describe().Should().NotContain("a-real-secret");
    }

    [Fact]
    public async Task EveryCredentialPresent_Passes()
    {
        var config = new AgentSmithConfig
        {
            Registries = [new RegistryConfig("feed.example.com", "any", "a-real-secret")],
        };

        var finding = await new RegistryCredentialCheck(config)
            .RunAsync(new PipelineContext(), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
        finding.Describe().Should().NotContain("a-real-secret");
    }

    [Fact]
    public async Task NoRegistriesConfigured_Passes()
    {
        var finding = await new RegistryCredentialCheck(AgentSmithConfig.Empty())
            .RunAsync(new PipelineContext(), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
        finding.Message.Should().Contain("nothing to stage");
    }
}
