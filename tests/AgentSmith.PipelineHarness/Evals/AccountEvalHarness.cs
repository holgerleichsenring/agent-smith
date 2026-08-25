using System.Text;
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
/// Everything between the fixture and the answer is production code: the delivery diff is
/// taken the way a run takes it, the repositories are combined into one account the way
/// PhaseAccounting combines them, and the search tool is handed the same live sandboxes.
/// Only the model call and the sandbox implementation differ from a live run, and the second
/// of those is what CLI mode already uses.
/// </para>
/// </summary>
public sealed class AccountEvalHarness(
    ISpecAccountant accountant, ILoggerFactory loggerFactory)
{
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

        var diff = await CombinedDiffAsync(repositories, ct);
        if (diff is null)
            return new AccountEvalReport.FixtureEntry(
                fixture.Id, fixture.Class, [], "the delivery diff could not be taken");

        var account = await accountant.AccountAsync(
            string.Join(", ", repositories.Sandboxes.Keys),
            [.. fixture.Criteria.Where(c => c.HasKnownTruth).Select(c => c.Text)],
            diff,
            fixture.Commands,
            agent,
            new BranchSearch(repositories.Sandboxes, loggerFactory.CreateLogger<BranchSearch>()),
            new PipelineCostTracker(),
            ct,
            fixture.WindowBudgetChars ?? DiffWindows.DefaultBudgetChars);

        return account.Problem is not null
            ? new AccountEvalReport.FixtureEntry(fixture.Id, fixture.Class, [], account.Problem)
            : new AccountEvalReport.FixtureEntry(
                fixture.Id, fixture.Class, Outcomes(fixture, account), null);
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
                    criterion.Text, criterion.IsMet, row?.Satisfied ?? false,
                    row?.Citation, row?.Note ?? (row is null ? "the account did not answer" : null));
            }),
        ];
    }

    /// <summary>The delivery diff of every repository, headed by its name — the shape
    /// PhaseAccounting builds, so the account reads what a run would hand it.</summary>
    private async Task<string?> CombinedDiffAsync(
        AccountFixtureRepositories repositories, CancellationToken ct)
    {
        var deliveryDiff = new DeliveryDiff(
            new SandboxBaseBranch(loggerFactory.CreateLogger<SandboxBaseBranch>()),
            loggerFactory.CreateLogger<DeliveryDiff>());

        var combined = new StringBuilder();
        foreach (var (name, sandbox) in repositories.Sandboxes)
        {
            var diff = await deliveryDiff.ForBranchAsync(sandbox, ct);
            if (diff.Failed) return null;
            combined.Append("# repository: ").Append(name).Append('\n')
                .Append(diff.Text).Append('\n');
        }
        return combined.ToString();
    }
}
