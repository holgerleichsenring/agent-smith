using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-11-25-098f: a pusher outside any run — it records no head.</summary>
public static class TestPushers
{
    public static GitBranchPusher Plain() => new(new NoOpEventPublisher(), new AsyncLocalRunContextAccessor());
}
