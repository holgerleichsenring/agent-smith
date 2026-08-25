using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Services;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-08-25-61f1: composes the event applier with its real projections for the many tests
/// that want "the applier as the composition root builds it". It used to be spelled out as a
/// row of positional <c>new()</c>s at twenty-odd call sites, so every projection extracted
/// from the applier cost twenty edits and told the reader nothing.
/// </summary>
internal static class RunEventAppliers
{
    public static RunEventApplier Default(ICapacityBudget? budget = null) =>
        new(
            checkpoints: new(),
            expectations: new(),
            queuedRuns: new(),
            sandboxes: new(new(), new()),
            steps: new(new()),
            pullRequests: new(),
            classification: new(),
            finalization: new(new(), budget),
            phases: new(),
            llmCalls: new(new(), new(), new()),
            decisions: new(new()));
}
