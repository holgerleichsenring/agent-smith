using System.Net;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0292: GitLabSourceProvider.ProbeAsync — a read-only GET against the project
/// endpoint. 2xx → Ok; any auth/transport failure → Ok=false with an Error and
/// NO exception escaping (the probe contract).
/// </summary>
public sealed class GitLabSourceProviderProbeTests
{
    private const string BaseUrl = "https://gitlab.example.com";
    private const string ProjectPath = "group%2Frepo";
    private const string CloneUrl = "https://gitlab.example.com/group/repo.git";
    private const string Token = "glpat-test";

    [Fact]
    public async Task ProbeAsync_ProjectReachable_ReturnsOk()
    {
        var handler = new FakeHandler(req =>
        {
            req.RequestUri!.AbsolutePath.Should().Contain("/api/v4/projects/group%2Frepo");
            req.Headers.GetValues("PRIVATE-TOKEN").Should().ContainSingle(t => t == Token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });

        var result = await CreateSut(handler).ProbeAsync(CancellationToken.None);

        result.Ok.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProbeAsync_Unauthorized_ReturnsFailureWithoutThrowing()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await CreateSut(handler).ProbeAsync(CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProbeAsync_TransportFailure_ReturnsFailureWithoutThrowing()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("connection refused"));

        var result = await CreateSut(handler).ProbeAsync(CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    private static GitLabSourceProvider CreateSut(HttpMessageHandler handler) =>
        new(new GitLabSourceConnection(BaseUrl, ProjectPath, CloneUrl, Token, null),
            new HttpClient(handler),
            NullLogger<GitLabSourceProvider>.Instance);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
