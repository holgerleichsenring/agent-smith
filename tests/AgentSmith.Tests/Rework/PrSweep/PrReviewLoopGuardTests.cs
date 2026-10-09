using System.Net;
using AgentSmith.Contracts.Models.Lifecycle;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Rework.PrSweep;

/// <summary>2026-10-09-af10: the pieces the sweep reads to never review its own commit, and to count failures.</summary>
public sealed class PrReviewLoopGuardTests
{
    [Fact]
    public void WipCommit_TheSubjectThePersistWrites_IsOurs() =>
        WipCommit.IsOurs($"{WipCommit.Subject("0f3c")}\n\nRun-Id: 0f3c\n").Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("fix: drop temp table")]
    [InlineData("Merge [wip] agent-smith run 0f3c")]
    public void WipCommit_AnyOtherMessage_IsNotOurs(string? message) => WipCommit.IsOurs(message).Should().BeFalse();

    [Fact]
    public async Task DetachedLauncher_ARunThatCannotStart_ReportsAFailure()
    {
        var outcome = new TaskCompletionSource<bool>();
        var launcher = new DetachedPipelineLauncher(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new ServerContext("c.yml"), NullLogger<DetachedPipelineLauncher>.Instance);

        await launcher.LaunchAsync("p", "pr-review", null, ok => { outcome.SetResult(ok); return Task.CompletedTask; });

        (await outcome.Task.WaitAsync(TestWaits.Hang)).Should().BeFalse();
    }

    [Fact]
    public async Task GitLabLister_HeadCommitMessage_ReadsTheCommit()
    {
        var handler = new OneAnswer("""{"id":"abc","message":"[wip] agent-smith run 1\n\nRun-Id: 1\n"}""");
        var lister = new GitLabOpenPullRequests("https://gitlab.example", "g%2Fr", "t", new HttpClient(handler));

        var message = await lister.HeadCommitMessageAsync("abc", CancellationToken.None);

        WipCommit.IsOurs(message).Should().BeTrue();
        handler.Asked!.AbsolutePath.Should().EndWith("/repository/commits/abc");
    }

    private sealed class OneAnswer(string json) : HttpMessageHandler
    {
        public Uri? Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Asked = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }
}
