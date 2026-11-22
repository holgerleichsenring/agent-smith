using System.Security.Claims;
using AgentSmith.Contracts.Runs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AgentSmith.Tests.Rework.Retry;

/// <summary>
/// 2026-11-22-50d7a: a Retry names the person who asked by the name their token shows them by, never by the opaque
/// sub; a token with no display name records no one, which the dashboard reads as "someone".
/// </summary>
public sealed class RetryAttributionTests : IDisposable
{
    private const string Sub = "XfJZvzB7NFo-XA3yf";

    private readonly RetryHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Retry_TokenWithAName_TheCauseNamesThePerson()
    {
        await _h.AttemptAsync("r-1", "cancelled", "interrupted");

        await AcceptedAsync(new Claim("sub", Sub), new Claim("name", "Ada Lovelace"));

        (await _h.RequestsAsync()).Single().RequestedBy.Should().Be("Ada Lovelace");
        await _h.HandleNextAsync();
        var cause = await _h.CauseOfNextAttemptAsync("r-2");
        cause.Channel.Should().Be(AttemptChannels.Retry);
        cause.Actor.Should().Be("Ada Lovelace");
    }

    [Fact]
    public async Task Retry_TokenWithOnlyASub_TheCauseNamesNoOne()
    {
        await _h.AttemptAsync("r-1", "cancelled", "interrupted");

        await AcceptedAsync(new Claim("sub", Sub));

        (await _h.RequestsAsync()).Single().RequestedBy.Should().BeEmpty("the sub is no name a person reads");
        await _h.HandleNextAsync();
        var cause = await _h.CauseOfNextAttemptAsync("r-2");
        cause.Channel.Should().Be(AttemptChannels.Retry);
        cause.Actor.Should().BeNull();
    }

    private async Task AcceptedAsync(params Claim[] claims) =>
        (await _h.RetryAsAsync("r-1", claims)).Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status202Accepted);
}
