using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0360b: Sandbox.Wire cannot reference Infrastructure, so the active-run set
// key is duplicated as a literal. This pin is the compile-adjacent guarantee
// that the agent's run-alive idle guard reads the SAME set the server writes —
// a silent divergence would make every idle probe answer "not active" and
// bring back the 1-hour sandbox suicide.
public sealed class ActiveRunsSetKeyConsistencyTests
{
    [Fact]
    public void AgentAndServer_UseTheSameActiveRunsSetKey() =>
        RedisKeys.ActiveRunsSet.Should().Be(EventStreamKeys.ActiveRunsSet);
}
