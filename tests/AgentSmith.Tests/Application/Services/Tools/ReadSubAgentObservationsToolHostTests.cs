using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using FluentAssertions;

namespace AgentSmith.Tests.SubAgents;

/// <summary>
/// p0280: read_sub_agent_observations returns a child's final answer from the in-memory
/// IChildAnswerStore (the functional channel; the never-built Redis event reader is gone).
/// </summary>
public sealed class ReadSubAgentObservationsToolHostTests
{
    [Fact]
    public void ReadSubAgentObservations_ReturnsChildAnswerFromStore()
    {
        var store = new InMemoryChildAnswerStore();
        store.Store("sa-1", "[{\"severity\":\"high\",\"description\":\"child finding\"}]");
        store.Store("sa-2", "other-child-answer");
        var sut = new ReadSubAgentObservationsToolHost(store);

        var result = sut.ReadObservations("sa-1");

        result.Should().Contain("child finding");
        result.Should().NotContain("other-child-answer");
    }

    [Fact]
    public void ReadSubAgentObservations_UnknownId_ReturnsNote()
    {
        var sut = new ReadSubAgentObservationsToolHost(new InMemoryChildAnswerStore());

        sut.ReadObservations("sa-missing").Should().Contain("no answer recorded");
    }

    [Fact]
    public void ReadSubAgentObservations_EmptyId_ReturnsError()
    {
        var sut = new ReadSubAgentObservationsToolHost(new InMemoryChildAnswerStore());

        sut.ReadObservations("").Should().Contain("required");
    }
}
