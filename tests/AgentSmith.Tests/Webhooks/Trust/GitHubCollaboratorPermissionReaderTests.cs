using System.Net;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Webhooks.Trust;

/// <summary>2026-10-25-9dbbe: the permission review is asked of the repository's own host with its own secret.</summary>
public sealed class GitHubCollaboratorPermissionReaderTests
{
    private static readonly RepoConnection Repo = new() { Name = "r", Url = "https://ghe.example/o/r.git", Auth = "gh_a" };

    private readonly Mock<IRepoCollaboratorsClient> _collaborators = new();
    private GitHubRepoAddress? _address;

    private GitHubCollaboratorPermissionReader CreateSut()
    {
        var repositories = new Mock<IRepositoriesClient>();
        repositories.SetupGet(r => r.Collaborator).Returns(_collaborators.Object);
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Repository).Returns(repositories.Object);
        var factory = new Mock<IGitHubClientFactory>();
        factory.Setup(f => f.Create(It.IsAny<GitHubRepoAddress>(), "secret"))
            .Callback<GitHubRepoAddress, string>((a, _) => _address = a).Returns(client.Object);
        return new GitHubCollaboratorPermissionReader(TestCredentials.With(("gh_a", "secret")), factory.Object);
    }

    [Fact]
    public async Task ReadAsync_Collaborator_ReturnsRoleAndPermissionFromTheRepositoryHost()
    {
        _collaborators.Setup(c => c.ReviewPermission("o", "r", "dev"))
            .ReturnsAsync(new CollaboratorPermissionResponse("write", "maintain", null!));

        var roles = await CreateSut().ReadAsync(Repo, "dev", CancellationToken.None);

        roles.Should().BeEquivalentTo("maintain", "write");
        _address!.Api.Rest.Should().Be(new Uri("https://ghe.example/api/v3/"));
    }

    [Fact]
    public async Task ReadAsync_NotFound_Empty()
    {
        _collaborators.Setup(c => c.ReviewPermission("o", "r", "dev"))
            .ThrowsAsync(new NotFoundException("Not Found", HttpStatusCode.NotFound));

        (await CreateSut().ReadAsync(Repo, "dev", CancellationToken.None)).Should().BeEmpty();
    }
}
