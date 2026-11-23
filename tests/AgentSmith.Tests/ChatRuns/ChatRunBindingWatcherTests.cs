using Microsoft.Extensions.DependencyInjection;
using AgentSmith.Server.Services.Hosting;
using AgentSmith.Server.Services.ChatRuns;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Hosting;

namespace AgentSmith.Tests.ChatRuns;

public sealed class ChatRunBindingWatcherTests
{
    [Fact]
    public async Task Watcher_OnStart_ReportsARunThatEndedWhileNoOneWasWatching()
    {
        using var chat = new ChatRunHarness();
        await ChatRunBindingTests.StartAsync(chat, new ChatThread("slack", "C1", "t1", "U1"), "run-1");
        chat.SeedRun("run-1", "success", finished: true, "done");
        // 2026-11-22-747db: registered behind its launcher; the case drives the worker itself.
        chat.Get<IEnumerable<IHostedService>>().OfType<WorkerLauncher<ChatRunBindingWatcher>>().Should().ContainSingle();
        var watcher = ActivatorUtilities.CreateInstance<ChatRunBindingWatcher>(chat.Get<IServiceProvider>());

        await watcher.StartAsync(CancellationToken.None);
        var reported = await TestWaits.ReachedAsync(() => chat.Slack.Posts.Any(p => p.Text.Contains("finished")));
        await watcher.StopAsync(CancellationToken.None);

        reported.Should().BeTrue("every replica follows the open bindings from startup on");
    }
}
