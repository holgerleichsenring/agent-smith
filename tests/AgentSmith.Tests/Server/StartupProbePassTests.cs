using AgentSmith.Server.Services.Startup;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-29-7f2d: the probes run after the bind, so the findings endpoint waits for their first pass —
/// and only as long as its budget, because a read that never answers is worse than one that answers early.
/// </summary>
public sealed class StartupProbePassTests
{
    [Fact]
    public async Task Wait_ThePassCompletes_Returns()
    {
        var pass = new StartupProbePass();
        var waiting = pass.WaitAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);
        waiting.IsCompleted.Should().BeFalse("nothing has completed the pass yet");

        pass.Complete();

        await waiting;
    }

    [Fact]
    public async Task Wait_ThePassNeverCompletes_ReturnsAfterTheBudget()
    {
        var pass = new StartupProbePass();

        // A spent budget is the answer, not a failure: the read goes on with what has been recorded. Spent
        // from the start, so nothing here waits on the clock.
        var spent = TimeSpan.Zero;
        await pass.WaitAsync(spent, CancellationToken.None);
    }
}
