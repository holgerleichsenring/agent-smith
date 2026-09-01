using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-25-7035: runs the REAL delivery account over each fixture delivery and scores its
/// dispositions against the truth the fixture declares.
/// <para>
/// Everything between the fixture and the answer is production code: the evidence is
/// gathered by <see cref="DeliveryEvidence"/>, the same gatherer a run gathers with, the
/// repositories are combined into one account the way PhaseAccounting combines them, and the
/// search tool is handed the same live sandboxes and the same base refs. Only the model call
/// and the sandbox implementation differ from a live run, and the second of those is what
/// CLI mode already uses.
/// </para>
/// <para>
/// 2026-08-28-c310: it gathered with a private copy of that loop until this phase, and the
/// copy threw the base ref away — so the account under test had no <c>search_base</c> and a
/// baseline taken here would have certified a defect.
/// </para>
/// </summary>
public sealed class AccountEvalHarness(
    ISpecAccountant accountant, ILoggerFactory loggerFactory)
{
    private readonly DeliveryDiff _deliveryDiff = new(
        new SandboxBaseBranch(loggerFactory.CreateLogger<SandboxBaseBranch>()),
        new SandboxRunStartCommit(loggerFactory.CreateLogger<SandboxRunStartCommit>()),
        loggerFactory.CreateLogger<DeliveryDiff>());

    public async Task<AccountEvalReport> RunAsync(
        IReadOnlyList<AccountFixture> fixtures, AgentConfig agent, string modelId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fixtures);
        var entries = new List<AccountEvalReport.FixtureEntry>();
        foreach (var fixture in fixtures)
            entries.Add(await ScoreAsync(fixture, agent, cancellationToken));
        return new AccountEvalReport(
            modelId, AccountPromptVersion.Current, DateTimeOffset.UtcNow, entries);
    }

    private async Task<AccountEvalReport.FixtureEntry> ScoreAsync(
        AccountFixture fixture, AgentConfig agent, CancellationToken ct)
    {
        await using var repositories =
            await AccountFixtureRepositories.MaterialiseAsync(fixture, loggerFactory, ct);
        return await ScoreAsync(fixture, repositories, agent, ct);
    }

    /// <summary>
    /// Scores one fixture over repositories that are already standing — the seam a test uses
    /// to score a delivery whose clone names no base, which no fixture file can express
    /// because the fixture builder always writes one.
    /// </summary>
    public async Task<AccountEvalReport.FixtureEntry> ScoreAsync(
        AccountFixture fixture, AccountFixtureRepositories repositories,
        AgentConfig agent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(repositories);

        var evidence = await DeliveryEvidence.GatherAsync(
            _deliveryDiff, repositories.Sandboxes, runId: null, ct);
        if (evidence.Failures.Count > 0)
            return new AccountEvalReport.FixtureEntry(
                fixture.Id, fixture.Class, [],
                $"the delivery diff could not be taken for {string.Join("; ", evidence.Failures)}");

        var account = await AccountForAsync(fixture, repositories, evidence, agent, ct);
        return account.Problem is not null
            ? new AccountEvalReport.FixtureEntry(fixture.Id, fixture.Class, [], account.Problem)
            : new AccountEvalReport.FixtureEntry(
                fixture.Id, fixture.Class, Outcomes(fixture, account), null);
    }

    /// <summary>The account itself, over the gathered evidence and a search that reaches the
    /// base wherever the diff resolved one.</summary>
    private async Task<SpecAccount> AccountForAsync(
        AccountFixture fixture, AccountFixtureRepositories repositories,
        DeliveryEvidence.Gathered evidence, AgentConfig agent, CancellationToken ct)
    {
        var search = new BranchSearch(
            repositories.Sandboxes, loggerFactory.CreateLogger<BranchSearch>(), evidence.BaseRefs);
        AccountToolParity.Verify(evidence.BaseRefs, search);

        return await accountant.AccountAsync(
            string.Join(", ", repositories.Sandboxes.Keys),
            [.. fixture.Criteria.Where(c => c.HasKnownTruth).Select(c => c.Text)],
            evidence.Diff, fixture.Commands, agent, search, new PipelineCostTracker(), ct,
            fixture.WindowBudgetChars ?? DiffWindows.DefaultBudgetChars);
    }

    /// <summary>
    /// A criterion the account said nothing about is scored as NOT satisfied — the same
    /// reading the gate applies, because silence is what the run acts on.
    /// </summary>
    private static IReadOnlyList<AccountEvalReport.CriterionOutcome> Outcomes(
        AccountFixture fixture, SpecAccount account)
    {
        var rows = account.Criteria.ToDictionary(
            row => row.Criterion, StringComparer.OrdinalIgnoreCase);
        return
        [
            .. fixture.Criteria.Where(c => c.HasKnownTruth).Select(criterion =>
            {
                rows.TryGetValue(criterion.Text, out var row);
                return new AccountEvalReport.CriterionOutcome(
                    criterion.Text, criterion.IsMet,
                    row?.Disposition ?? AccountDisposition.NotSatisfied,
                    row?.Citation, row?.Note ?? (row is null ? "the account did not answer" : null));
            }),
        ];
    }
}
