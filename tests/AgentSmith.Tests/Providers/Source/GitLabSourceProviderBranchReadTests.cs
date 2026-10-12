using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-10-12-6bfaa: GitLab's named-branch reads. Existence is false ONLY on a 404 — a 401 read as
/// "absent" would hide a credential failure behind a silent fall to the default branch.
/// </summary>
public sealed class GitLabSourceProviderBranchReadTests
{
    private static readonly BranchName Ticket = new("agent-smith/4711");

    [Fact]
    public async Task BranchExists_404IsAbsent_200IsPresent()
    {
        (await Sut(HttpStatusCode.NotFound).BranchExistsAsync(Ticket, CancellationToken.None)).Should().BeFalse();
        (await Sut(HttpStatusCode.OK).BranchExistsAsync(Ticket, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task BranchExists_AuthError_Throws()
    {
        var act = async () => await Sut(HttpStatusCode.Unauthorized).BranchExistsAsync(Ticket, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ReadsOnABranch_SendTheBranchAsTheRef()
    {
        var queries = new List<string>();
        var sut = Sut(HttpStatusCode.OK, req => queries.Add(req.RequestUri!.ToString()), "[{\"name\":\"default\"}]");

        var names = await sut.ListDirectoryAsync(".agentsmith/contexts", Ticket, CancellationToken.None);
        await sut.TryReadFileAsync(".agentsmith/contexts/default/context.yaml", Ticket, CancellationToken.None);

        names.Should().Equal("default");
        queries.Should().HaveCount(2).And.OnlyContain(q => q.Contains("ref=agent-smith%2F4711"));
    }

    private static GitLabSourceProvider Sut(
        HttpStatusCode status, Action<HttpRequestMessage>? seen = null, string body = "") =>
        new(new GitLabSourceConnection("https://gitlab.example.com", "group%2Frepo",
                "https://gitlab.example.com/group/repo.git", "glpat-test", "main"),
            new HttpClient(new FakeHandler(req =>
            {
                seen?.Invoke(req);
                return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8) };
            })),
            NullLogger<GitLabSourceProvider>.Instance);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
