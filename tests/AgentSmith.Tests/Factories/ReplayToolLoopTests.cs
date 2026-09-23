using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// 2026-09-23-e848: a replayed run wraps the tasks production wraps. Its own copy of the set
/// omitted Reasoning, so a recorded reasoning call replayed with no tool loop at all — the
/// recording could be re-driven to a pass that the run it stands for could not reach.
/// <para>
/// The four-way comparison this belongs to lives in the harness, the only assembly that can
/// see the two harness factories as well. This one names the task that was missing.
/// </para>
/// </summary>
public sealed class ReplayToolLoopTests
{
    [Fact]
    public void ReplayFactory_ReasoningTask_GrantsTheToolLoop()
    {
        var factory = new ReplayChatClientFactory(new StubChatClient(new Queue<string>()));

        factory.Create(Agent(), TaskType.Reasoning)
            .Should().BeOfType<FunctionInvokingChatClient>(
                "production grants Reasoning a tool loop, so a replay of the same task has to "
                + "invoke the recorded tool calls rather than hand back the bare client");
    }

    private static AgentConfig Agent() => new()
    {
        Type = "stub",
        Model = "m",
        Models = new ModelRegistryConfig(),
    };
}
