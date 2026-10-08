using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class AzureDevOpsPrCommentWebhookHandlerTests
{
    private const string RepoUrl = "https://dev.azure.com/org/MyProject/_git/my-api";
    private const string ProjectId = "11111111-1111-1111-1111-111111111111";
    private const string RepositoryId = "22222222-2222-2222-2222-222222222222";
    private const string AuthorId = "33333333-3333-3333-3333-333333333333";
    private const int Read = 2;
    private const int ReadAndContribute = 2 | 4;

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly PrCommentHandlerFixture _fixture = new();
    private readonly Mock<IAzureDevOpsPermissionReader> _permissions = new();

    private AzureDevOpsPrCommentWebhookHandler CreateSut() =>
        new(_fixture.Admission(),
            new AzureDevOpsRepoContributeTrust(PrCommentHandlerFixture.Repos(RepoUrl), _permissions.Object,
                PrCommentHandlerFixture.NewCache(), NullLogger<AzureDevOpsRepoContributeTrust>.Instance),
            NullLogger<AzureDevOpsPrCommentWebhookHandler>.Instance);

    private void Effective(int allow, int deny = 0) =>
        _permissions.Setup(p => p.ReadAsync(It.IsAny<RepoConnection>(),
                "https://dev.azure.com/org", new Guid("2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87"),
                It.Is<IReadOnlyList<string>>(t => t[0] == $"repoV2/{ProjectId}/{RepositoryId}"),
                new Guid(AuthorId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureDevOpsEffectivePermission(allow, deny));

    private static string Comment(string content) => $$"""
        {
            "eventType": "ms.vss-code.git-pullrequest-comment-event",
            "resource": {
                "comment": {
                    "id": 300, "content": "{{content}}",
                    "author": { "id": "{{AuthorId}}", "uniqueName": "dev@org.com" }
                },
                "pullRequest": {
                    "pullRequestId": 58,
                    "repository": {
                        "id": "{{RepositoryId}}", "name": "my-api",
                        "remoteUrl": "https://org@dev.azure.com/org/MyProject/_git/my-api",
                        "project": { "id": "{{ProjectId}}", "name": "MyProject" }
                    }
                }
            }
        }
        """;

    [Fact]
    public void CanHandle_CorrectEventTypes()
    {
        var sut = CreateSut();

        sut.CanHandle("azuredevops", "ms.vss-code.git-pullrequest-comment-event").Should().BeTrue();
        sut.CanHandle("azuredevops", "workitem.updated").Should().BeFalse();
        sut.CanHandle("github", "ms.vss-code.git-pullrequest-comment-event").Should().BeFalse();
    }

    [Fact]
    public async Task AzureDevOpsPrComment_WithContribute_StartsPipeline()
    {
        Effective(ReadAndContribute);

        var result = await CreateSut().HandleAsync(Comment("/agent-smith fix #99 in payments"), EmptyHeaders);

        result.Handled.Should().BeTrue();
        _fixture.LaunchedAs().Should().Be("code #99 pr:MyProject/my-api#58");
    }

    [Fact]
    public async Task AzureDevOpsPrComment_WithoutContribute_IsNotHandled_AndParserNeverCalled()
    {
        Effective(Read);

        var result = await CreateSut().HandleAsync(Comment("/agent-smith fix"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _fixture.VerifyModelNeverAsked();
    }

    [Fact]
    public async Task AzureDevOpsPrComment_ContributeDenied_IsNotHandled()
    {
        Effective(ReadAndContribute, deny: 4);

        var result = await CreateSut().HandleAsync(Comment("/agent-smith fix"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _fixture.VerifyModelNeverAsked();
    }

    [Fact]
    public async Task AzureDevOpsPrComment_AclLookupFails_IsNotHandled()
    {
        _permissions.Setup(p => p.ReadAsync(It.IsAny<RepoConnection>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("no Security scope"));

        var result = await CreateSut().HandleAsync(Comment("/agent-smith fix"), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _fixture.VerifyModelNeverAsked();
    }

    [Theory]
    [InlineData("Just a regular comment")]
    [InlineData("/agent-smith help")]
    [InlineData("/approve")]
    public async Task AzureDevOpsPrComment_WithoutACommand_IsNotHandled_AndCostsNoLookup(string content)
    {
        var result = await CreateSut().HandleAsync(Comment(content), EmptyHeaders);

        result.Handled.Should().BeFalse();
        _permissions.VerifyNoOtherCalls();
        _fixture.VerifyModelNeverAsked();
    }
}
