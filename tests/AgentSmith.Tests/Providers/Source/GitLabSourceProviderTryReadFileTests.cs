using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0135: GitLabSourceProvider.TryReadFileAsync — hits the raw-file REST endpoint
/// with the project-path + url-encoded file-path + default-branch ref. 200 →
/// content, 404 → null, anything-else → throw (5xx, 401, 403).
/// </summary>
public sealed class GitLabSourceProviderTryReadFileTests
{
    private const string BaseUrl = "https://gitlab.example.com";
    private const string ProjectPath = "group/repo";
    private const string CloneUrl = "https://gitlab.example.com/group/repo.git";
    private const string Token = "glpat-test";
    private const string DefaultBranch = "main";

    [Fact]
    public async Task TryReadFileAsync_FileExists_ReturnsContent()
    {
        var handler = new FakeHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Get);
            req.RequestUri!.AbsolutePath.Should().Contain("/api/v4/projects/group%2Frepo/repository/files/.agentsmith%2Fcontext.yaml/raw");
            req.Headers.GetValues("PRIVATE-TOKEN").Should().ContainSingle(t => t == Token);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("stack:\n  lang: C#\n", Encoding.UTF8, "text/plain")
            };
        });
        var sut = CreateSut(handler);

        var result = await sut.TryReadFileAsync(".agentsmith/context.yaml", CancellationToken.None);

        result.Should().Be("stack:\n  lang: C#\n");
    }

    [Fact]
    public async Task TryReadFileAsync_FileNotFound_ReturnsNull()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);

        var result = await sut.TryReadFileAsync(".agentsmith/context.yaml", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryReadFileAsync_ServerError_Throws()
    {
        // 5xx is "server is broken", not "file not present" — must surface so the
        // caller doesn't silently fall through to the next resolution layer
        // when the API itself is degraded.
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = CreateSut(handler);

        var act = async () => await sut.TryReadFileAsync(".agentsmith/context.yaml", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task TryReadFileAsync_AuthError_Throws()
    {
        // 401 = bad/missing token. This is a config error and must throw so the
        // operator sees it rather than silently falling back to generic image.
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var sut = CreateSut(handler);

        var act = async () => await sut.TryReadFileAsync(".agentsmith/context.yaml", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static GitLabSourceProvider CreateSut(HttpMessageHandler handler)
    {
        return new GitLabSourceProvider(
            connection: new GitLabSourceConnection(BaseUrl, ProjectPath, CloneUrl, Token, DefaultBranch),
            httpClient: new HttpClient(handler),
            logger: NullLogger<GitLabSourceProvider>.Instance);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
