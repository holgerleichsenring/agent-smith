using System.Net;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.Services.Common;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-10-14-dbcek: a required ticket is read once on the project's tracker; each tracker's not-found is
/// missing, any other failure is unreadable with its class and never the SDK's text.</summary>
public sealed class TicketExistenceTests
{
    [Fact]
    public async Task TicketExistence_AzureDevOpsTf401232_IsMissing()
    {
        var result = await CheckAsync(new VssServiceException("TF401232: Work item 41 does not exist, or you do not have permissions to read it."));

        result.Missing.Should().Equal("41");
        result.Unreadable.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketExistence_GitHubNotFound_IsMissing() =>
        (await CheckAsync(new Octokit.NotFoundException("gone", HttpStatusCode.NotFound))).Missing.Should().Equal("41");

    [Fact]
    public async Task TicketExistence_JiraAndGitLabNotFound_AreMissing()
    {
        (await CheckAsync(new HttpRequestException("gone", null, HttpStatusCode.NotFound))).Missing.Should().Equal("41");
        (await CheckAsync(new TicketNotFoundException(new TicketId("41")))).Missing.Should().Equal("41");
    }

    [Fact]
    public async Task TicketExistence_Unauthorized_IsUnreadableWithItsClassAndNoSdkText()
    {
        var result = await CheckAsync(new HttpRequestException("401 at https://tracker.internal.example", null, HttpStatusCode.Unauthorized));

        result.Missing.Should().BeEmpty();
        result.Unreadable.Should().Equal(new UnreadableTicket("41", "unauthorized"));
    }

    [Fact]
    public async Task TicketExistence_FoundAndRepeated_ReadOnceAndNothingReported()
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ticket(new TicketId("41"), "t", "d", null, "open", "test"));

        var result = await Existence(provider).CheckAsync(new ResolvedProject(), ["41", "41"], CancellationToken.None);

        result.Missing.Should().BeEmpty();
        result.Unreadable.Should().BeEmpty();
        provider.Verify(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task<TicketExistenceResult> CheckAsync(Exception failure)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        return await Existence(provider).CheckAsync(new ResolvedProject(), ["41"], CancellationToken.None);
    }

    private static TicketExistence Existence(Mock<ITicketProvider> provider)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        return new TicketExistence(factory.Object, NullLogger<TicketExistence>.Instance);
    }
}
