using AgentSmith.Contracts.Events;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Events;

/// <summary>
/// p0367: the coalescer turns the per-tool-call SandboxCommand firehose into at
/// most one liveness rollup per run per second. The first command emits at once;
/// commands inside the window only accumulate the count.
/// </summary>
public sealed class SandboxActivityCoalescerTests
{
    private const string RunId = "run-1";
    private const string Repo = "default";

    [Fact]
    public void Observe_FirstCommand_EmitsRollupImmediately()
    {
        var clock = new FixedClock();
        var coalescer = new SandboxActivityCoalescer(clock);

        var rollup = coalescer.Observe(RunId, Command("dotnet"));

        rollup.Should().NotBeNull();
        rollup!.Commands.Should().Be(1);
        rollup.LastCommand.Should().Be("dotnet");
    }

    [Fact]
    public void Observe_WithinInterval_ThrottlesThenAccumulates()
    {
        var clock = new FixedClock();
        var coalescer = new SandboxActivityCoalescer(clock);

        coalescer.Observe(RunId, Command("a")).Should().NotBeNull();       // emit at T0
        clock.Now = clock.Now.AddMilliseconds(400);
        coalescer.Observe(RunId, Command("b")).Should().BeNull();          // throttled
        clock.Now = clock.Now.AddMilliseconds(800);                        // now > 1s since emit
        var rollup = coalescer.Observe(RunId, Command("c"));

        rollup.Should().NotBeNull();
        rollup!.Commands.Should().Be(2, "b and c accumulated since the last emit");
        rollup.LastCommand.Should().Be("c");
    }

    private static SandboxCommandEvent Command(string command) =>
        new(RunId, Repo, command, 1, DateTimeOffset.UtcNow);

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
