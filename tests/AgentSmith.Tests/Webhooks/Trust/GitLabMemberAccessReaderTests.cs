using System.Net;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Webhooks.Trust;

public sealed class GitLabMemberAccessReaderTests
{
    private static readonly RepoConnection Repo = new()
    {
        Name = "r", Type = RepoType.GitLab, Url = "https://gitlab.example.com/org/r", Auth = "gitlab_a",
    };

    [Fact]
    public async Task ReadAccessLevelAsync_AMember_ReturnsTheLevelFromTheMembersAllEndpoint()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{ "id": 42, "access_level": 30 }""");

        var level = await Reader(handler).ReadAccessLevelAsync(Repo, "7", "42", CancellationToken.None);

        level.Should().Be(30);
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be("https://gitlab.example.com/api/v4/projects/7/members/all/42");
        handler.LastRequest.Headers.Contains("PRIVATE-TOKEN").Should().BeTrue();
    }

    // 2026-10-02-5f89a: the repository's own auth secret, not a token picked by type.
    [Fact]
    public async Task GitLabMemberAccessReader_UsesTheConfiguredRepositorysSecret()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{ "id": 42, "access_level": 30 }""");
        var reader = Reader(handler, ("gitlab_a", "token-a"), ("gitlab_b", "token-b"));

        await reader.ReadAccessLevelAsync(Repo with { Auth = "gitlab_b" }, "7", "42", CancellationToken.None);

        handler.LastRequest!.Headers.GetValues("PRIVATE-TOKEN").Should().Equal("token-b");
    }

    [Fact]
    public async Task ReadAccessLevelAsync_RepoHost_IsTheInstanceAsked()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{ "id": 42, "access_level": 30 }""");

        await Reader(handler).ReadAccessLevelAsync(
            Repo with { Host = "https://gitlab.example.com/sub" }, "7", "42", CancellationToken.None);

        handler.LastRequest!.RequestUri!.ToString()
            .Should().StartWith("https://gitlab.example.com/sub/api/v4/");
    }

    [Fact]
    public async Task ReadAccessLevelAsync_NoMember_ReturnsNull()
    {
        var level = await Reader(new StubHandler(HttpStatusCode.NotFound, "{}"))
            .ReadAccessLevelAsync(Repo, "7", "42", CancellationToken.None);

        level.Should().BeNull();
    }

    [Fact]
    public async Task ReadAccessLevelAsync_AnyOtherFailure_Throws()
    {
        var act = () => Reader(new StubHandler(HttpStatusCode.Forbidden, "{}"))
            .ReadAccessLevelAsync(Repo, "7", "42", CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    private static GitLabMemberAccessReader Reader(
        HttpMessageHandler handler, params (string, string)[] secrets)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, false));
        var catalog = secrets.Length > 0 ? secrets : [("gitlab_a", "test-token")];
        return new GitLabMemberAccessReader(TestCredentials.With(catalog), factory.Object);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
