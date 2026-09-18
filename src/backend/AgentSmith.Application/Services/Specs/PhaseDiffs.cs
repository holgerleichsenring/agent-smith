using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: what THIS phase changed, per sandbox — from the head
/// <see cref="PhaseStartHeads"/> recorded when the phase was selected to the working tree.
/// <para>
/// To the working tree, not to HEAD: the master commits as it goes and the pre-gate commit
/// puts the rest on the branch, but a phase may also stand with edits the checkpoint refused,
/// and the admission reads the same files the reviewer reads. <c>git diff &lt;head&gt;</c> with
/// no second revision is exactly that interval — the form <see cref="DeliveryDiff"/> uses.
/// </para>
/// </summary>
public sealed class PhaseDiffs(DeliveryDiff deliveryDiff, ILogger<PhaseDiffs> logger)
{
    private const int GitTimeoutSeconds = 120;

    /// <summary>What the review is shown, and what its findings may rest on.</summary>
    public async Task<IReadOnlyList<PhaseDiff>> TakeAsync(
        PipelineContext pipeline, IReadOnlyDictionary<string, ISandbox> sandboxes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(sandboxes);
        var basis = DeliveryBasis.For(pipeline);
        var diffs = new List<PhaseDiff>();
        foreach (var (key, sandbox) in sandboxes)
        {
            var head = PhaseStartHeads.For(pipeline, key);
            var (text, from) = head is not null
                ? (await DiffFromAsync(sandbox, head, cancellationToken), $"the commit this phase started at ({Short(head)})")
                // A sandbox opened mid-phase has no start head. Its whole branch delivery is
                // the honest answer, and the reviewer is told that is what it is looking at.
                : ((await deliveryDiff.ForBranchAsync(sandbox, basis, cancellationToken)).Text,
                    "the branch's whole delivery — no head was recorded when this phase started, "
                    + "so some of what follows may belong to an earlier phase");
            var bound = PhaseDiffText.Bound(PhaseDiffText.Files(text), PhaseDiffText.MaxChars);
            logger.LogInformation(
                "{Key}: phase diff from {From} — {Shown} file(s) shown, {Unreviewed} past the bound",
                key, from, bound.Paths.Count, bound.Unreviewed.Count);
            diffs.Add(new PhaseDiff(key, bound.Text, bound.Paths, bound.Unreviewed, from));
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
/// One sandbox's phase diff as the reviewer is shown it.
/// </summary>
/// <param name="Paths">The files inside the bound — the only paths a finding may name.</param>
/// <param name="Unreviewed">The files past it, named so the reviewer states nothing about them.</param>
/// <param name="From">What the diff was taken against, in the prompt's own words.</param>
public sealed record PhaseDiff(
    string SandboxKey,
    string Text,
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> Unreviewed,
    string From)
{
    public bool IsEmpty => Paths.Count == 0;
}
