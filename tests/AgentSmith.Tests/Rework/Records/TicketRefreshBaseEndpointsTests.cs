using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-11-24-9dc0e: Try again's endpoint — an unknown project is 404, no URL 400, a refusal 409 with its sentence,
/// a start 200 with the run id.</summary>
public sealed class TicketRefreshBaseEndpointsTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private readonly Mock<IConfigurationLoader> _loader = new();

    public TicketRefreshBaseEndpointsTests() =>
        _loader.Setup(l => l.LoadConfig(It.IsAny<string>()))
            .Returns(new AgentSmithConfig { Projects = new Dictionary<string, ResolvedProject> { ["p"] = Project } });

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TryAgainAsync_UnknownProject_NoUrl_AndARefusal_SayWhy()
    {
        (await StatusAsync("other", "https://x/pull/1")).Should().Be((StatusCodes.Status404NotFound, "No project other is configured."));
        (await StatusAsync("p", " ")).Status.Should().Be(StatusCodes.Status400BadRequest);
        (await StatusAsync("p", Pr1)).Should().Be((StatusCodes.Status409Conflict, $"7 has no open pull request {Pr1}."));
    }

    private async Task<(int Status, string Text)> StatusAsync(string project, string url)
    {
        var result = await TicketRefreshBaseEndpoints.TryAgainAsync(project, "7", new RefreshBaseRequest(url),
            new TicketProjects(_loader.Object, new ServerContext("c.yml")), _h.Retry(), CancellationToken.None);
        var text = result.Should().BeOfType<ContentHttpResult>().Subject;
        return (text.StatusCode ?? 200, text.ResponseContent ?? string.Empty);
    }
}
