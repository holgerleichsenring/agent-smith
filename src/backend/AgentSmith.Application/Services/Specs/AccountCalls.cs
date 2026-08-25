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
/// Both callers get the same complete file list. The correction demands a path copied
/// exactly as the FILE LIST prints it, so a correction shown one window's list was asking a
/// criterion whose file lives elsewhere to comply with a list that cannot hold it.
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
        IReadOnlyList<string> windows, IReadOnlyList<string> commandResults,
        IReadOnlyList<string>? searchable, IList<AITool>? tools, CitedFileIndex deliveryFiles,
        IReadOnlyList<string>? baseSearchable, PipelineCostTracker costTracker, CancellationToken ct)
    {
        var answers = new List<IReadOnlyList<AccountRow>>();
        foreach (var window in windows.Count == 0 ? [string.Empty] : windows)
        {
            var rows = await call.AskAsync(
                chat, repoKey, criteria, window, searchable, commandResults, costTracker, ct,
                tools: tools, deliveryFiles: deliveryFiles, baseSearchable: baseSearchable);
            if (rows is not null) answers.Add(rows);
        }
        return answers.Count == 0 ? null : AccountWindowMerge.Of(answers);
    }

    /// <summary>The correction pass: the same evidence, plus the objection. Never a verdict —
    /// a criterion reported not satisfied is an answer, not a formatting failure.</summary>
    public Task<IReadOnlyList<AccountRow>?> AskCorrectionAsync(
        IChatClient chat, string repoKey, IReadOnlyList<string> criteria, string window,
        IReadOnlyList<string>? searchable, IReadOnlyList<string> commandResults,
        IList<AITool>? tools, CitedFileIndex deliveryFiles, string correction,
        IReadOnlyList<string>? baseSearchable, PipelineCostTracker costTracker, CancellationToken ct) =>
        call.AskAsync(
            chat, repoKey, criteria, window, searchable, commandResults, costTracker, ct,
            correction, tools, deliveryFiles, baseSearchable);
}
