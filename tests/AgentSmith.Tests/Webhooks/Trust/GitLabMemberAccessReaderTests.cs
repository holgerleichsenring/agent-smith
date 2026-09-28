using System.Net;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Webhooks.Trust;

[Collection(EnvVarCollection.Name)]
public sealed class GitLabMemberAccessReaderTests
{
    [Fact]
    public async Task ReadAccessLevelAsync_AMember_ReturnsTheLevelFromTheMembersAllEndpoint()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{ "id": 42, "access_level": 30 }""");

        var level = await WithToken(() => Reader(handler).ReadAccessLevelAsync(
            "https://gitlab.example.com/org/r", "7", "42", CancellationToken.None));

        level.Should().Be(30);
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be("https://gitlab.example.com/api/v4/projects/7/members/all/42");
        handler.LastRequest.Headers.Contains("PRIVATE-TOKEN").Should().BeTrue();
    }

    [Fact]
    public async Task ReadAccessLevelAsync_NoMember_ReturnsNull()
    {
        var level = await WithToken(() => Reader(new StubHandler(HttpStatusCode.NotFound, "{}"))
            .ReadAccessLevelAsync("https://gitlab.example.com/org/r", "7", "42", CancellationToken.None));

        level.Should().BeNull();
    }

    [Fact]
    public async Task ReadAccessLevelAsync_AnyOtherFailure_Throws()
    {
        var act = () => WithToken(() => Reader(new StubHandler(HttpStatusCode.Forbidden, "{}"))
            .ReadAccessLevelAsync("https://gitlab.example.com/org/r", "7", "42", CancellationToken.None));

        await act.Should().ThrowAsync<Exception>();
    }

    private static GitLabMemberAccessReader Reader(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, false));
        return new GitLabMemberAccessReader(new SecretsProvider(), factory.Object);
    }

    private static async Task<T> WithToken<T>(Func<Task<T>> act)
    {
        var (token, url) = (Environment.GetEnvironmentVariable("GITLAB_TOKEN"), Environment.GetEnvironmentVariable("GITLAB_URL"));
        Environment.SetEnvironmentVariable("GITLAB_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("GITLAB_URL", null);
        try { return await act(); }
        finally
        {
            Environment.SetEnvironmentVariable("GITLAB_TOKEN", token);
            Environment.SetEnvironmentVariable("GITLAB_URL", url);
        }
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
