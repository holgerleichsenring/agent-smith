using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SubAgents;

public sealed class ReadSubAgentObservationsToolHostTests
{
    [Fact]
    public async Task ReadSubAgentObservations_ReturnsPagedBusEvents_NotSummary()
    {
        var events = new RunEvent[]
        {
            new SubAgentObservationEvent("run-1", "sa-1", "first", DateTimeOffset.UtcNow),
            new SubAgentObservationEvent("run-1", "sa-1", "second", DateTimeOffset.UtcNow),
            new SubAgentObservationEvent("run-1", "sa-2", "other-child", DateTimeOffset.UtcNow),
        };
        var sut = new ReadSubAgentObservationsToolHost(new InMemoryEventReader(events), "run-1");
        var tool = sut.GetTools(phase: null, investigatorMode: null).Single();

        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["sub_agent_id"] = "sa-1",
        });

        var resultStr = result.ToString()!;
        resultStr.Should().Contain("first");
        resultStr.Should().Contain("second");
        resultStr.Should().NotContain("other-child");
    }

    [Fact]
    public async Task ReadSubAgentObservations_FilterByKind_ReturnsOnlyMatching()
    {
        var events = new RunEvent[]
        {
            new SubAgentObservationEvent("run-1", "sa-1", "an observation", DateTimeOffset.UtcNow),
            new SubAgentFindingEvent("run-1", "sa-1", "high", "the title", "the detail", DateTimeOffset.UtcNow),
            new SubAgentToolCallEvent("run-1", "sa-1", "read_file", "read_file foo.cs", DateTimeOffset.UtcNow),
        };
        var sut = new ReadSubAgentObservationsToolHost(new InMemoryEventReader(events), "run-1");
        var tool = sut.GetTools(phase: null, investigatorMode: null).Single();

        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["sub_agent_id"] = "sa-1",
            ["kinds"] = new[] { "finding" },
        });

        var resultStr = result.ToString()!;
        resultStr.Should().Contain("the title");
        resultStr.Should().NotContain("an observation");
        resultStr.Should().NotContain("read_file");
    }

    private sealed class InMemoryEventReader(IReadOnlyList<RunEvent> events) : IRunEventReader
    {
        public Task<IReadOnlyList<RunEvent>> ReadAsync(string runId, CancellationToken cancellationToken)
            => Task.FromResult(events);
    }
}
