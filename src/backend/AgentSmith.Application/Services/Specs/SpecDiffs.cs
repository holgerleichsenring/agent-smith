using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: what THIS spec changed, per sandbox — from the head
/// <see cref="SpecStartHeads"/> recorded when the spec was selected to the working tree.
/// <para>
/// To the working tree, not to HEAD: the master commits as it goes and the pre-gate commit
/// puts the rest on the branch, but a spec may also stand with edits the checkpoint refused,
/// and the admission reads the same files the reviewer reads. <c>git diff &lt;head&gt;</c> with
/// no second revision is exactly that interval — the form <see cref="DeliveryDiff"/> uses.
/// </para>
/// </summary>
public sealed class SpecDiffs(DeliveryDiff deliveryDiff, ILogger<SpecDiffs> logger)
{
    private const int GitTimeoutSeconds = 120;

    /// <summary>What the review is shown, and what its findings may rest on.</summary>
    public async Task<IReadOnlyList<SpecDiff>> TakeAsync(
        PipelineContext pipeline, IReadOnlyDictionary<string, ISandbox> sandboxes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(sandboxes);
        var basis = DeliveryBasis.For(pipeline);
        var diffs = new List<SpecDiff>();
        foreach (var (key, sandbox) in sandboxes)
        {
            var head = SpecStartHeads.For(pipeline, key);
            var (text, from) = head is not null
                ? (await DiffFromAsync(sandbox, head, cancellationToken), $"the commit this spec started at ({Short(head)})")
                // A sandbox opened mid-spec has no start head. Its whole branch delivery is
                // the honest answer, and the reviewer is told that is what it is looking at.
                : ((await deliveryDiff.ForBranchAsync(sandbox, basis, cancellationToken)).Text,
                    "the branch's whole delivery — no head was recorded when this spec started, "
                    + "so some of what follows may belong to an earlier spec");
            var bound = SpecDiffText.Bound(SpecDiffText.Files(text), SpecDiffText.MaxChars);
            logger.LogInformation(
                "{Key}: spec diff from {From} — {Shown} file(s) shown, {Unreviewed} past the bound",
                key, from, bound.Paths.Count, bound.Unreviewed.Count);
            diffs.Add(new SpecDiff(key, bound.Text, bound.Paths, bound.Unreviewed, from));
        }
        return diffs;
    }

    private static async Task<string> DiffFromAsync(ISandbox sandbox, string head, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: ["diff", "--no-color", head], WorkingDirectory: "/work",
                TimeoutSeconds: GitTimeoutSeconds),
            progress: null, ct);
        return result.ExitCode == 0 ? result.OutputContent ?? string.Empty : string.Empty;
    }

    private static string Short(string sha) => sha.Length <= 8 ? sha : sha[..8];
}

/// <summary>
/// One sandbox's spec diff as the reviewer is shown it.
/// </summary>
/// <param name="Paths">The files inside the bound — the only paths a finding may name.</param>
/// <param name="Unreviewed">The files past it, named so the reviewer states nothing about them.</param>
/// <param name="From">What the diff was taken against, in the prompt's own words.</param>
public sealed record SpecDiff(
    string SandboxKey,
    string Text,
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> Unreviewed,
    string From)
{
    public bool IsEmpty => Paths.Count == 0;
}
