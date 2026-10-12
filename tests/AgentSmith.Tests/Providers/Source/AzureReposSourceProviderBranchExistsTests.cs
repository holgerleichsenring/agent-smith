using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-10-12-6bfaa: Azure Repos' refs filter matches by PREFIX, so a ticket branch whose name is
/// the start of another's must not be read as existing when only the longer one does.
/// </summary>
public sealed class AzureReposSourceProviderBranchExistsTests
{
    [Fact]
    public async Task BranchExists_OnlyTheExactRefCounts()
    {
        var sut = Sut("refs/heads/agent-smith/123");

        (await sut.BranchExistsAsync(new BranchName("agent-smith/12"), CancellationToken.None)).Should().BeFalse();
        (await sut.BranchExistsAsync(new BranchName("agent-smith/123"), CancellationToken.None)).Should().BeTrue();
    }

    private static AzureReposSourceProvider Sut(params string[] refs)
    {
        var git = new Mock<GitHttpClient>(
            new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        git.Setup(c => c.GetRefsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refs.Select(r => new GitRef { Name = r }).ToList());
        var factory = new Mock<IAzDoClientFactory>();
        factory.Setup(f => f.CreateGitClient(It.IsAny<string>(), It.IsAny<string>())).Returns(git.Object);
        return new AzureReposSourceProvider(
            new AzureReposSourceConnection("https://dev.azure.com/example", "demo", "repo", "pat", DefaultBranch: "main"),
            factory.Object, NullLogger<AzureReposSourceProvider>.Instance);
    }
}
