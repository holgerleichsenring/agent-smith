using Microsoft.Extensions.AI;
using AgentSmith.Application.Services;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-1360: how many calls an account makes, and what each one is shown.
/// <para>
/// Separated from <see cref="SpecAccountant"/> because deciding how a delivery is SLICED,
/// and what each slice is handed, is not the same question as what an account is worth once
/// taken — and holding both in one type is what put it past the length the architecture rule
/// allows.
/// </para>
/// <para>
/// Every caller gets the same complete file list, carried by <see cref="AccountEvidence"/>.
/// The correction demands a path copied exactly as the FILE LIST prints it, so a correction
/// shown one window's list was asking a criterion whose file lives elsewhere to comply with a
/// list that cannot hold it.
/// </para>
/// </summary>
public sealed class AccountCalls(SpecAccountCall call)
{
    /// <summary>
    /// Every window is asked; <see cref="AccountWindowMerge"/> decides what their answers
    /// mean together. A window that could not see the evidence is a statement about that
    /// slice, never about the branch.
    /// </summary>
    public async Task<IReadOnlyList<AccountRow>?> AskEveryAsync(
        IChatClient chat, string repoKey, IReadOnlyList<string> criteria,
        IReadOnlyList<string> windows, AccountEvidence evidence,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(windows);
        var answers = new List<IReadOnlyList<AccountRow>>();
        foreach (var window in windows.Count == 0 ? [string.Empty] : windows)
        {
            var rows = await AskAsync(chat, repoKey, criteria, window, evidence, costTracker, ct);
            if (rows is not null) answers.Add(rows);
        }
        return answers.Count == 0 ? null : AccountWindowMerge.Of(answers);
    }

    /// <summary>The correction pass: the same evidence, plus the objection. Never a verdict —
    /// a criterion reported not satisfied is an answer, not a formatting failure.</summary>
    public Task<IReadOnlyList<AccountRow>?> AskCorrectionAsync(
        IChatClient chat, string repoKey, IReadOnlyList<string> criteria, string window,
        AccountEvidence evidence, string correction,
        PipelineCostTracker costTracker, CancellationToken ct) =>
        AskAsync(chat, repoKey, criteria, window, evidence, costTracker, ct, correction);

    /// <summary>
    /// 2026-08-25-6f12: the full-reach pass — NO diff body at all. A criterion whose subjects
    /// were split across windows is unanswerable from any one window's body by construction,
    /// so the body is the one thing this call does not carry: it is settled from the complete
    /// file list and from searching the branch and its base.
    /// </summary>
    public Task<IReadOnlyList<AccountRow>?> AskWithFullReachAsync(
        IChatClient chat, string repoKey, IReadOnlyList<string> criteria,
        AccountEvidence evidence, string instruction,
        PipelineCostTracker costTracker, CancellationToken ct) =>
        AskAsync(chat, repoKey, criteria, string.Empty, evidence, costTracker, ct, instruction);

    private Task<IReadOnlyList<AccountRow>?> AskAsync(
        IChatClient chat, string repoKey, IReadOnlyList<string> criteria, string window,
        AccountEvidence evidence, PipelineCostTracker costTracker, CancellationToken ct,
        string? appended = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return call.AskAsync(
            chat, repoKey, criteria, window, evidence.Searchable, evidence.CommandResults,
            costTracker, ct, appended, evidence.Tools, evidence.DeliveryFiles,
            evidence.BaseSearchable);
    }
}
