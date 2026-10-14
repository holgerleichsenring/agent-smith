using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-10-13-8762n: no reviewer runs after entry, so the master's prompt is where a stale citation is told.</summary>
public sealed class StaleCitationPromptTests
{
    [Fact]
    public void SpecExecutionPrompt_StaleCitations_AreListed()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, SpecBuildNoteTests.Draft("2026-10-14-5678a"));
        pipeline.Set<IReadOnlyList<string>>(ContextKeys.StaleCitations,
            ["src/Api/Orders.cs:2-3 (shop): its lines changed since 1a2b3c4d and their text was found nowhere"]);

        var prompt = new SpecExecutionPromptFactory().Build(pipeline,
            new Ticket(new TicketId("7"), "t", "d", null, "open", "test"),
            new Repository(new BranchName("main"), "https://example.test/shop.git"), ["shop"], string.Empty, string.Empty);

        prompt.Should().Contain("their text was found nowhere: verify the claim before relying on it")
            .And.Contain("- src/Api/Orders.cs:2-3 (shop): its lines changed since 1a2b3c4d");
        pipeline.Set<IReadOnlyList<string>>(ContextKeys.StaleCitations, []);
        SpecExecutionPromptBlocks.StaleCitations(pipeline).Should().BeEmpty();
    }
}
