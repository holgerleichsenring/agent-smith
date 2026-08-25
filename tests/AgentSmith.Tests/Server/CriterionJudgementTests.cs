using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-25-e257: the operator records that a criterion's disposition was wrong.
/// <para>
/// A run that fails on a wrong criterion is indistinguishable from one that fails on a right
/// one. Fourteen phases have tuned the delivery account, each on a single failed run, because
/// the only failures that announce themselves are mechanical. This is the first place the
/// other kind can be written down.
/// </para>
/// </summary>
public sealed class CriterionJudgementTests
{
    private const string Run = "2026-08-25T09-23-17-67ea";
    private const string Criterion = "Each applicable host declares explicit publish routes.";

    private static CriterionJudgementRequest Overrule(string criterion = Criterion) =>
        new(criterion, AcceptanceCriterionStatuses.Unmet, AcceptanceCriterionStatuses.Met,
            "the extension declares six routes; the window that judged it never saw the file");

    [Fact]
    public async Task Judgement_ACriterionMarkedWrong_IsStoredAgainstTheRun()
    {
        using var store = MigratedStoreTemplate.OpenCopy();
        using var ctx = MigratedStoreTemplate.Context(store);
        var repository = new CriterionJudgementRepository(ctx);

        await repository.RecordAsync(Run, Overrule(), "holger", DateTimeOffset.UnixEpoch, default);

        var judgement = (await repository.ForRunAsync(Run, default)).Should().ContainSingle().Subject;
        judgement.Criterion.Should().Be(Criterion);
        judgement.MachineStatus.Should().Be(AcceptanceCriterionStatuses.Unmet);
        judgement.HumanStatus.Should().Be(AcceptanceCriterionStatuses.Met);
        judgement.Author.Should().Be("holger");
        judgement.Reason.Should().NotBeEmpty();
    }

    /// <summary>A judgement is a current opinion, not a history nobody reads.</summary>
    [Fact]
    public async Task Judgement_RecordedTwice_ReplacesRatherThanAppends()
    {
        using var store = MigratedStoreTemplate.OpenCopy();
        using var ctx = MigratedStoreTemplate.Context(store);
        var repository = new CriterionJudgementRepository(ctx);

        await repository.RecordAsync(Run, Overrule(), "holger", DateTimeOffset.UnixEpoch, default);
        await repository.RecordAsync(
            Run,
            new CriterionJudgementRequest(
                Criterion, AcceptanceCriterionStatuses.Unmet,
                AcceptanceCriterionStatuses.NotApplicable, "the base never had that transport"),
            "holger", DateTimeOffset.UnixEpoch.AddHours(1), default);

        (await repository.ForRunAsync(Run, default)).Should().ContainSingle()
            .Which.HumanStatus.Should().Be(AcceptanceCriterionStatuses.NotApplicable);
    }

    /// <summary>
    /// The criteria of a re-derived phase can reorder. A label keyed by position would move
    /// to a different criterion, which is worse than no label at all.
    /// </summary>
    [Fact]
    public async Task Judgement_WhenTheCriteriaReorder_StaysWithItsCriterion()
    {
        using var store = MigratedStoreTemplate.OpenCopy();
        using var ctx = MigratedStoreTemplate.Context(store);
        var repository = new CriterionJudgementRepository(ctx);

        await repository.RecordAsync(Run, Overrule("first criterion"), "holger", DateTimeOffset.UnixEpoch, default);
        await repository.RecordAsync(Run, Overrule("second criterion"), "holger", DateTimeOffset.UnixEpoch, default);

        var judgements = await repository.ForRunAsync(Run, default);
        judgements.Select(j => j.Criterion)
            .Should().BeEquivalentTo(["first criterion", "second criterion"]);
    }

    /// <summary>Whitespace is not a different criterion; wording is.</summary>
    [Fact]
    public void CriterionKey_DiffersOnlyInWhitespace_IsTheSameCriterion()
    {
        CriterionKey.Of("a  criterion\n  that wraps")
            .Should().Be(CriterionKey.Of("a criterion that wraps"));
        CriterionKey.Of("a criterion").Should().NotBe(CriterionKey.Of("another criterion"));
    }

    [Fact]
    public async Task Judgement_Withdrawn_IsGone()
    {
        using var store = MigratedStoreTemplate.OpenCopy();
        using var ctx = MigratedStoreTemplate.Context(store);
        var repository = new CriterionJudgementRepository(ctx);
        await repository.RecordAsync(Run, Overrule(), "holger", DateTimeOffset.UnixEpoch, default);

        (await repository.WithdrawAsync(Run, Criterion, default)).Should().BeTrue();

        (await repository.ForRunAsync(Run, default)).Should().BeEmpty();
        (await repository.WithdrawAsync(Run, Criterion, default)).Should().BeFalse(
            "withdrawing what is not there is not an error, it is a no");
    }

    /// <summary>
    /// The reason the judgement lives in its own row: the story applier assigns the
    /// acceptance payload wholesale on every publish, so a resume, a retry or a repair pass
    /// would silently destroy a label stored inside it.
    /// </summary>
    [Fact]
    public async Task Judgement_AfterTheAcceptanceSnapshotIsReplaced_Survives()
    {
        using var store = MigratedStoreTemplate.OpenCopy();
        using var ctx = MigratedStoreTemplate.Context(store);
        var repository = new CriterionJudgementRepository(ctx);
        ctx.Runs.Add(new Run { Id = Run, AcceptanceJson = """{"criteria":[],"outcome":"verbatim","ratifiedBy":"x"}""" });
        await ctx.SaveChangesAsync();
        await repository.RecordAsync(Run, Overrule(), "holger", DateTimeOffset.UnixEpoch, default);

        var run = ctx.Runs.Single(r => r.Id == Run);
        run.AcceptanceJson = """{"criteria":[],"outcome":"edited","ratifiedBy":"y"}""";
        await ctx.SaveChangesAsync();

        var served = await repository.AcceptanceForRunAsync(Run, default);
        served.Acceptance!.Outcome.Should().Be("edited");
        served.Judgements.Should().ContainSingle(
            "a judgement about a snapshot has to outlive the snapshot");
    }

    [Fact]
    public async Task Acceptance_AndItsJudgements_AreServedTogether()
    {
        using var store = MigratedStoreTemplate.OpenCopy();
        using var ctx = MigratedStoreTemplate.Context(store);
        var repository = new CriterionJudgementRepository(ctx);
        ctx.Runs.Add(new Run
        {
            Id = Run,
            AcceptanceJson = """{"criteria":[{"text":"c","status":"unmet","reason":null}],"outcome":"verbatim","ratifiedBy":"x","source":"delivery_account"}""",
        });
        await ctx.SaveChangesAsync();
        await repository.RecordAsync(Run, Overrule("c"), "holger", DateTimeOffset.UnixEpoch, default);

        var served = await repository.AcceptanceForRunAsync(Run, default);

        served.Acceptance!.Criteria.Should().ContainSingle().Which.Text.Should().Be("c");
        served.Acceptance.Source.Should().Be(AcceptanceSources.DeliveryAccount);
        served.Judgements.Should().ContainSingle().Which.Criterion.Should().Be("c");
    }

    /// <summary>
    /// A label without a reason cannot be audited later, and an unauditable label is worse
    /// than none — so the reason is refused at the door rather than defaulted.
    /// </summary>
    [Fact]
    public void Judgement_WithoutAReason_IsRefused() =>
        AgentSmith.Server.Extensions.CriterionJudgementEndpoints.Invalid(
            new CriterionJudgementRequest(
                Criterion, AcceptanceCriterionStatuses.Unmet, AcceptanceCriterionStatuses.Met, "  "))
            .Should().Contain("states why");

    [Fact]
    public void Judgement_WithoutACriterion_IsRefused() =>
        AgentSmith.Server.Extensions.CriterionJudgementEndpoints.Invalid(
            new CriterionJudgementRequest(
                "", AcceptanceCriterionStatuses.Unmet, AcceptanceCriterionStatuses.Met, "because"))
            .Should().Contain("names its criterion");

    /// <summary>A corpus that scores dispositions cannot read a status it does not know.</summary>
    [Fact]
    public void Judgement_WithAnUnknownDisposition_IsRefused() =>
        AgentSmith.Server.Extensions.CriterionJudgementEndpoints.Invalid(
            new CriterionJudgementRequest(Criterion, "wrong", AcceptanceCriterionStatuses.Met, "because"))
            .Should().Contain("not a known disposition");

    /// <summary>An overrule that agrees with the account is not a correction; it is noise in
    /// the corpus that would read as a labelled disagreement.</summary>
    [Fact]
    public void Judgement_AgreeingWithTheAccount_IsRefused() =>
        AgentSmith.Server.Extensions.CriterionJudgementEndpoints.Invalid(
            new CriterionJudgementRequest(
                Criterion, AcceptanceCriterionStatuses.Met, AcceptanceCriterionStatuses.Met, "because"))
            .Should().Contain("differs from the account");

    [Fact]
    public void Judgement_Complete_IsAccepted() =>
        AgentSmith.Server.Extensions.CriterionJudgementEndpoints.Invalid(Overrule())
            .Should().BeNull();

    [Fact]
    public async Task Acceptance_ForARunWithNoSnapshot_ServesNothingRatherThanFailing()
    {
        using var store = MigratedStoreTemplate.OpenCopy();
        using var ctx = MigratedStoreTemplate.Context(store);

        var served = await new CriterionJudgementRepository(ctx)
            .AcceptanceForRunAsync("no-such-run", default);

        served.Acceptance.Should().BeNull();
        served.Judgements.Should().BeEmpty();
    }
}
