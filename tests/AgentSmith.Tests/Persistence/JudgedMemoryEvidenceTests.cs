using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-25-9324c: a person's not-met judgement counts against the memories the criterion's spec relied on, with
/// the person's reason; a met judgement adds nothing and a withdrawn one takes its contradiction back.
/// </summary>
public sealed class JudgedMemoryEvidenceTests : IDisposable
{
    private const string RunId = "2026-10-25T1200-judged";
    private const string Criterion = "The checkout total includes tax.";
    private const string Reason = "the total on the page leaves the tax out";
    private static readonly DateTimeOffset T = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private readonly ServerStateStore _store = new();

    public JudgedMemoryEvidenceTests()
    {
        using var db = _store.Context();
        db.Set<RunSpec>().AddRange(Spec("s-a", Criterion), Spec("s-b", "Something else holds."));
        db.Set<MemoryEvidence>().AddRange(Relied("s-a", "x"), Relied("s-a", "y"), Relied("s-b", "z"));
        db.SaveChanges();
    }

    private static RunSpec Spec(string id, string criterion) => new()
    {
        RunId = RunId, SpecId = id, Title = id, Status = "done",
        AcceptanceJson = RunStoryJson.Serialize(new AcceptanceView(
            [new AcceptanceCriterionView(criterion, AcceptanceCriterionStatuses.Met, null)], "met", "the gate")),
    };

    private static MemoryEvidence Relied(string spec, string slug) => new()
    {
        RepoKey = "github.com/o/api", Slug = slug, Kind = MemoryEvidenceKinds.Relied, Project = "sample", TicketId = "A-1",
        RunId = RunId, SpecId = spec, At = T,
    };

    private Task JudgeAsync(string humanStatus)
    {
        using var db = _store.Context();
        return new CriterionJudgementRepository(db, new()).RecordAsync(RunId,
            new CriterionJudgementRequest(Criterion, AcceptanceCriterionStatuses.Met, humanStatus, Reason), "ada", T, default);
    }

    [Fact]
    public async Task NotMetJudgement_ReliedMemories_ContradictedWithReason()
    {
        await JudgeAsync(AcceptanceCriterionStatuses.Unmet);
        await JudgeAsync(AcceptanceCriterionStatuses.Unmet);

        var contradicted = await ContradictionsAsync();
        contradicted.Select(e => e.Slug).Should().BeEquivalentTo(["x", "y"], "the judged criterion is s-a's, judged once");
        contradicted.Should().AllSatisfy(e =>
        {
            (e.Weight, e.Reason, e.SpecId, e.CriterionKey).Should().Be((-1d, Reason, "s-a", CriterionKey.Of(Criterion)));
        });
    }

    [Fact]
    public async Task WithdrawnJudgement_RetractsContradiction()
    {
        await JudgeAsync(AcceptanceCriterionStatuses.Unmet);

        await using (var db = _store.Context())
            (await new CriterionJudgementRepository(db, new()).WithdrawAsync(RunId, Criterion, default)).Should().BeTrue();

        (await ContradictionsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task MetJudgement_AddsNothing_AndReplacesAnEarlierNotMet()
    {
        await JudgeAsync(AcceptanceCriterionStatuses.Unmet);
        await JudgeAsync(AcceptanceCriterionStatuses.Met);

        (await ContradictionsAsync()).Should().BeEmpty();
    }

    private async Task<List<MemoryEvidence>> ContradictionsAsync()
    {
        await using var db = _store.Context();
        return await db.Set<MemoryEvidence>().Where(e => e.Kind == MemoryEvidenceKinds.Contradicted).ToListAsync();
    }

    public void Dispose() => _store.Dispose();
}
