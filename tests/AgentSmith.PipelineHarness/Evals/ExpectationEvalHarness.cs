using AgentSmith.Application.Services.Expectations;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: replays each golden through the REAL <see cref="IExpectationDrafter"/>
/// (the p0328 drafting stage: same prompt resource, parser, validation
/// retries), then judges the draft against the human gold per assertion.
/// The fixture's context hints ride the pipeline context exactly like
/// AnalyzeCode's output does in a live run, so the draft is grounded the same
/// way. LLM-free tier scripts both calls; the eval tier pays for them.
/// </summary>
public sealed class ExpectationEvalHarness(
    IExpectationDrafter drafter, ExpectationEvalJudge judge)
{
    public async Task<ExpectationEvalReport> RunAsync(
        IReadOnlyList<ExpectationFixture> fixtures, AgentConfig agentConfig,
        string modelId, string skillsPin, CancellationToken cancellationToken)
    {
        var entries = new List<ExpectationEvalReport.FixtureEntry>();
        foreach (var fixture in fixtures)
            entries.Add(await EvaluateAsync(fixture, agentConfig, cancellationToken));
        return new ExpectationEvalReport(modelId, skillsPin, DateTimeOffset.UtcNow, entries);
    }

    private async Task<ExpectationEvalReport.FixtureEntry> EvaluateAsync(
        ExpectationFixture fixture, AgentConfig agentConfig, CancellationToken cancellationToken)
    {
        var goldCount = fixture.Gold!.Expected.Count;
        var (draft, error) = await drafter.DraftAsync(
            ToTicket(fixture), agentConfig, ToPipelineContext(fixture), cancellationToken);
        if (draft is null)
            return new ExpectationEvalReport.FixtureEntry(fixture.Id, goldCount, null, error);
        var verdict = await judge.JudgeAsync(fixture.Gold!, draft, cancellationToken);
        return new ExpectationEvalReport.FixtureEntry(fixture.Id, goldCount, verdict, null);
    }

    private static Ticket ToTicket(ExpectationFixture fixture) => new(
        $"golden-{fixture.Id}",
        fixture.Ticket!.Title!,
        fixture.Ticket!.Description!,
        fixture.Ticket!.AcceptanceCriteria,
        "open",
        "expectation-golden");

    private static PipelineContext ToPipelineContext(ExpectationFixture fixture)
    {
        var pipeline = new PipelineContext();
        if (!string.IsNullOrWhiteSpace(fixture.ContextHints?.CodeMap))
            pipeline.Set(ContextKeys.CodeMap, fixture.ContextHints!.CodeMap!);
        return pipeline;
    }
}
