using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Claim;

public sealed class NullTicketClaimServiceTests
{
    [Fact]
    public async Task ClaimAsync_AnyRequest_ReturnsFailedRedisUnavailable()
    {
        var sut = new NullTicketClaimService(NullLogger<NullTicketClaimService>.Instance);

        var result = await sut.ClaimAsync(Request(), Config(), CancellationToken.None);

        result.Outcome.Should().Be(ClaimOutcome.Failed);
        result.Error.Should().Be("redis_unavailable");
    }

    [Fact]
    public async Task ClaimAsync_CalledTwice_BothReturnFailed()
    {
        var sut = new NullTicketClaimService(NullLogger<NullTicketClaimService>.Instance);

        var first = await sut.ClaimAsync(Request(), Config(), CancellationToken.None);
        var second = await sut.ClaimAsync(Request(), Config(), CancellationToken.None);

        first.Outcome.Should().Be(ClaimOutcome.Failed);
        second.Outcome.Should().Be(ClaimOutcome.Failed);
        first.Error.Should().Be("redis_unavailable");
        second.Error.Should().Be("redis_unavailable");
    }

    private static ClaimRequest Request() => new(
        Platform: "GitHub",
        ProjectName: "p",
        TicketId: new TicketId("1"),
        PipelineName: "fix-bug",
        InitialContext: null);

    private static AgentSmithConfig Config()
    {
        var config = new AgentSmithConfig();
        config.Projects["p"] = new ProjectConfig { Tickets = new TicketConfig { Type = "github" } };
        return config;
    }
}
