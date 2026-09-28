using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks.Trust;

public sealed class AzureDevOpsRepoContributeTrustTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Repository = Guid.NewGuid();
    private static readonly Guid Author = Guid.NewGuid();

    private readonly Mock<IAzureDevOpsPermissionReader> _permissions = new();

    private AzureDevOpsRepoContributeTrust CreateSut(string configuredUrl) =>
        new(PrCommentHandlerFixture.Repos(configuredUrl), _permissions.Object,
            PrCommentHandlerFixture.NewCache(), NullLogger<AzureDevOpsRepoContributeTrust>.Instance);

    private static PrCommentAuthor TheAuthor(string? projectId = null) =>
        new("https://x/_git/r", Repository.ToString(), Author.ToString(), "dev@org.com")
        {
            ProjectId = projectId ?? Project.ToString(),
        };

    [Fact]
    public async Task IsTrustedAsync_AsksTheRepositoryTokenFirst_ThenItsParents()
    {
        IReadOnlyList<string>? asked = null;
        _permissions.Setup(p => p.ReadAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<string>>(), Author, It.IsAny<CancellationToken>()))
            .Callback((string _, Guid _, IReadOnlyList<string> tokens, Guid _, CancellationToken _) => asked = tokens)
            .ReturnsAsync(new AzureDevOpsEffectivePermission(4, 0));

        (await CreateSut("https://dev.azure.com/org/P/_git/r").IsTrustedAsync(TheAuthor(), default)).Should().BeTrue();

        asked.Should().Equal($"repoV2/{Project}/{Repository}", $"repoV2/{Project}", "repoV2");
    }

    [Fact]
    public async Task IsTrustedAsync_OnAzureDevOpsServer_AsksTheCollection()
    {
        _permissions.Setup(p => p.ReadAsync("https://tfs.example.com/tfs/DefaultCollection", It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<string>>(), Author, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureDevOpsEffectivePermission(4, 0));

        var sut = CreateSut("https://tfs.example.com/tfs/DefaultCollection/P/_git/r");

        (await sut.IsTrustedAsync(TheAuthor(), default)).Should().BeTrue();
    }

    [Fact]
    public async Task IsTrustedAsync_NoAclAnswers_IsUntrusted()
    {
        _permissions.Setup(p => p.ReadAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AzureDevOpsEffectivePermission?)null);

        (await CreateSut("https://dev.azure.com/org/P/_git/r").IsTrustedAsync(TheAuthor(), default)).Should().BeFalse();
    }

    [Fact]
    public async Task IsTrustedAsync_PayloadWithoutAProjectId_IsUntrusted()
    {
        var sut = CreateSut("https://dev.azure.com/org/P/_git/r");

        (await sut.IsTrustedAsync(TheAuthor(projectId: ""), default)).Should().BeFalse();
        _permissions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IsTrustedAsync_Twice_AsksAzureDevOpsOnce()
    {
        _permissions.Setup(p => p.ReadAsync(It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureDevOpsEffectivePermission(4, 0));
        var sut = CreateSut("https://dev.azure.com/org/P/_git/r");

        await sut.IsTrustedAsync(TheAuthor(), default);
        await sut.IsTrustedAsync(TheAuthor(), default);

        _permissions.Verify(p => p.ReadAsync(It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<IReadOnlyList<string>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
