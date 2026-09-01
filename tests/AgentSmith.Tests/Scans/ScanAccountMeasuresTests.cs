using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// 2026-09-01-3653: every number in the argument about the scan's prompt had been a guess —
/// fifty-two thousand characters, then seven and a half thousand for a skill that carries
/// three reference tokens the body resolver inlines. Two wrong numbers in one discussion,
/// from the same absence: nothing wrote it down. The account the scan already writes now
/// does, and the turn count result.md printed and always got wrong is gone.
/// </summary>
public sealed class ScanAccountMeasuresTests
{
    [Fact]
    public void ScanAccount_CarriesThePromptSizesThePassWasGiven()
    {
        var measures = Account(WithMaster()).Measures;

        measures.Should().NotBeNull();
        measures!.SystemPromptChars.Should().Be(12345);
        measures.ScannerFindingsChars.Should().BeGreaterThan(0, "the section was rendered");
        measures.OpenApiDocumentChars.Should().Be("OPENAPI_BODY".Length);
        measures.ConversationChars.Should().BeGreaterThan(0);
        measures.SurfaceDifferenceChars.Should().Be(0, "no surface difference was computed");
    }

    [Fact]
    public void ScanAccount_CarriesTurnsUsedAgainstTheCeiling()
    {
        var measures = Account(WithMaster()).Measures;

        measures!.TurnsUsed.Should().Be(7);
        measures.IterationCeiling.Should().Be(100);

        var rendered = SpecAccountRenderer.ToMarkdown([Account(WithMaster())]);
        rendered.Should().Contain("~7 turns against a ceiling of 100")
            .And.Contain("near-exact", "a provider may split one turn across messages");
    }

    [Fact]
    public void ScanAccount_CarriesTheDistinctReadCount()
    {
        var measures = Account(WithMaster()).Measures;

        measures!.DistinctReadCount.Should().Be(2, "the read-set is counted, not rebuilt");
        SpecAccountRenderer.ToMarkdown([Account(WithMaster())])
            .Should().Contain("2 distinct source file(s) read");
    }

    [Fact]
    public void ScanAccount_OfARunWithNoMaster_OmitsTheSectionRatherThanZeroingIt()
    {
        var pipeline = WithContract(new PipelineContext());

        var account = new ScanCoverageAccountant().Account(pipeline);

        account.Measures.Should().BeNull("a row of zeroes reads like a measurement");
        SpecAccountRenderer.ToMarkdown([account]).Should().NotContain("measured:");
    }

    [Fact]
    public void RunResult_DoesNotPrintTwoDisagreeingTurnCounts()
    {
        var summary = new RunCostSummary(
            new Dictionary<string, PhaseCost> { ["primary"] = new("m", 100, 50, 0, 0.1m) },
            0.1m);
        var ticket = new Ticket(new TicketId("1"), "Test", "Desc", null, "Open", "github");

        var result = RunResultFormatter.FormatResult(
            ticket, null, [], "run-1", 12, summary, null);

        result.Should().NotContain("turns:",
            "the field was fed from a limit enforcer whose recording method has no "
            + "production caller, so it was always zero — the account states the turns now");
        SpecAccountRenderer.ToMarkdown([Account(WithMaster())]).Should().Contain("turns");
    }

    [Fact]
    public void TurnCount_FromAMultiMessagePass_IsCountedFromAssistantMessages()
    {
        var response = new ChatResponse(
        [
            new ChatMessage(ChatRole.Assistant, "thinking"),
            new ChatMessage(ChatRole.Tool, "tool result"),
            new ChatMessage(ChatRole.Assistant, "answer"),
        ]);

        MasterTurnCount.From(response).Should().Be(2, "tool messages are not turns");
        MasterTurnCount.From(null).Should().Be(0);
    }

    private static SpecAccount Account(PipelineContext pipeline) =>
        new ScanCoverageAccountant().Account(pipeline);

    private static PipelineContext WithMaster()
    {
        var pipeline = WithContract(new PipelineContext());
        pipeline.Set(ContextKeys.MasterSystemPromptChars, 12345);
        pipeline.Set(ContextKeys.MasterTurnsUsed, 7);
        pipeline.Set(ContextKeys.ScanMasterIterationCeiling, 100);
        pipeline.Set(ContextKeys.MasterReadPaths,
            new List<string> { "src/A.cs", "src/B.cs", "src/A.cs" });
        pipeline.Set(ContextKeys.SwaggerSpec, new SwaggerSpec("api", "1.0", [], [], "OPENAPI_BODY"));
        pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments,
            [new TicketComment("author", DateTimeOffset.UtcNow, "use approach B")]);
        return pipeline;
    }

    private static PipelineContext WithContract(PipelineContext pipeline)
    {
        pipeline.Set(ContextKeys.ScanContract, new ScanContract(
        [
            new ScanCriterion("Every candidate finding is triaged by the scan master",
                CommandNames.AgenticMaster),
        ]));
        pipeline.Set(ContextKeys.ExecutionTrail, new List<ExecutionTrailEntry>
        {
            new(CommandNames.AgenticMaster, null, true, "master completed",
                DateTimeOffset.UtcNow, TimeSpan.Zero, null),
        });
        return pipeline;
    }
}
