using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-12-6bfaa: the branch a pre-sandbox context read happens on — the first candidate that
/// exists and declares contexts, else the default branch, with every candidate passed over named.
/// </summary>
public sealed class ContextBranchLocatorTests
{
    private const string Contexts = ".agentsmith/contexts";
    private static readonly BranchName Ticket = new("agent-smith/4711");
    private readonly Mock<ISourceProvider> _provider = new();
    private readonly ContextBranchLocator _sut = new(NullLogger<ContextBranchLocator>.Instance);

    [Fact]
    public async Task TicketBranchDeclaresContexts_ReadsThere()
    {
        _provider.Setup(p => p.BranchExistsAsync(Ticket, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _provider.Setup(p => p.ListDirectoryAsync(Contexts, Ticket, It.IsAny<CancellationToken>())).ReturnsAsync(["default"]);

        var directory = await _sut.LocateAsync(_provider.Object, ContextReadBranches.ForBranch(Ticket), CancellationToken.None);

        directory.Branch.Should().Be(Ticket);
        directory.Children.Should().Equal("default");
        directory.Where.Should().Be("branch 'agent-smith/4711'");
        _provider.Verify(p => p.ListDirectoryAsync(Contexts, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TicketBranchAbsent_FallsToDefault_AndSaysWhy()
    {
        _provider.Setup(p => p.BranchExistsAsync(Ticket, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _provider.Setup(p => p.ListDirectoryAsync(Contexts, It.IsAny<CancellationToken>())).ReturnsAsync(["api"]);

        var directory = await _sut.LocateAsync(_provider.Object, ContextReadBranches.ForBranch(Ticket), CancellationToken.None);

        directory.Branch.Should().BeNull();
        directory.Children.Should().Equal("api");
        directory.PassedOver.Should().Equal("branch 'agent-smith/4711' is not on the remote");
    }

    [Fact]
    public async Task TicketBranchWithoutContexts_FallsToDefault()
    {
        _provider.Setup(p => p.BranchExistsAsync(Ticket, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _provider.Setup(p => p.ListDirectoryAsync(Contexts, Ticket, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _provider.Setup(p => p.ListDirectoryAsync(Contexts, It.IsAny<CancellationToken>())).ReturnsAsync(["api"]);

        var directory = await _sut.LocateAsync(_provider.Object, ContextReadBranches.ForBranch(Ticket), CancellationToken.None);

        directory.Branch.Should().BeNull("an existing branch gets its base merged in at checkout");
        directory.PassedOver.Should().Equal("branch 'agent-smith/4711' has no .agentsmith/contexts/");
    }

    [Fact]
    public async Task UnreadableCandidate_IsPassedOverWithItsReason()
    {
        _provider.Setup(p => p.BranchExistsAsync(Ticket, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("401 Unauthorized"));
        _provider.Setup(p => p.ListDirectoryAsync(Contexts, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var directory = await _sut.LocateAsync(_provider.Object, ContextReadBranches.ForBranch(Ticket), CancellationToken.None);

        directory.Branch.Should().BeNull();
        directory.PassedOver.Should().ContainSingle().Which.Should().Contain("could not be read").And.Contain("401");
    }

    [Fact]
    public async Task NoCandidates_ReadsTheDefaultBranchOnly()
    {
        _provider.Setup(p => p.ListDirectoryAsync(Contexts, It.IsAny<CancellationToken>())).ReturnsAsync(["api"]);

        var directory = await _sut.LocateAsync(_provider.Object, ContextReadBranches.None, CancellationToken.None);

        directory.Where.Should().Be("the default branch");
        _provider.Verify(p => p.BranchExistsAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
