using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-11-24-629ab: the step arms auto-complete again through the provider that can; a host without one, a local
/// repository and a host failure say why and never throw.</summary>
public sealed class PullRequestAutoCompleteRearmTests
{
    private const string Url = "https://dev.azure.com/o/p/_git/r/pullrequest/7";
    private static readonly RepoConnection Azure = new() { Name = "r", Type = RepoType.AzureDevOps };

    [Fact]
    public async Task ARearmer_ArmsAgain()
    {
        var provider = new Mock<ISourceProvider>();
        provider.As<IPullRequestAutoCompleteRearmer>().Setup(r => r.RearmAutoCompleteAsync(Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletion.Armed("set"));

        (await Step(provider.Object).RearmAsync(Azure, Url, CancellationToken.None)).Outcome.Should().Be(PullRequestCompletionOutcome.Armed);
    }

    [Fact]
    public async Task AHostWithoutOne_OrALocalRepository_IsRefused()
    {
        (await Step(Mock.Of<ISourceProvider>()).RearmAsync(Azure, Url, CancellationToken.None))
            .Reason.Should().Contain("no auto-complete");
        (await Step(Mock.Of<ISourceProvider>()).RearmAsync(new RepoConnection { Name = "l", Type = RepoType.Local }, Url, CancellationToken.None))
            .Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
    }

    [Fact]
    public async Task AHostFailure_IsARefusal_WithItsMessage()
    {
        var provider = new Mock<ISourceProvider>();
        provider.As<IPullRequestAutoCompleteRearmer>().Setup(r => r.RearmAutoCompleteAsync(Url, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("host timeout"));

        (await Step(provider.Object).RearmAsync(Azure, Url, CancellationToken.None)).Reason.Should().Be("host timeout");
    }

    private static PullRequestAutoCompleteRearm Step(ISourceProvider provider) =>
        new(Mock.Of<ISourceProviderFactory>(f => f.Create(It.IsAny<RepoConnection>()) == provider), NullLogger<PullRequestAutoCompleteRearm>.Instance);
}
