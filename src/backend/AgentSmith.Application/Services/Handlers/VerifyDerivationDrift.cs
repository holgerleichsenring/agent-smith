using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-e14d: re-hashes the files a declaration was derived from and says when they
/// have moved.
/// <para>
/// A verify block is authored once and replayed unchanged, which is what makes it a second
/// opinion rather than the model marking its own work. The price of authoring once is that
/// the repository can change underneath it, so the declaration records its source and every
/// run reads those few files back. That is a filesystem read in the sandbox — no model call,
/// no re-derivation, nothing an estate has to pay for per run.
/// </para>
/// <para>
/// It REPORTS and stops. Re-deriving on a mismatch would put a model back in the position
/// of rewriting the gate it is about to be judged by, and a hash is not evidence that the
/// declaration is wrong — only that nobody has checked it since the pipeline last changed.
/// Whether to re-derive is the operator's call.
/// </para>
/// </summary>
public sealed class VerifyDerivationDrift(
    VerifyDerivationDigest digest, ILogger<VerifyDerivationDrift> logger)
{
    public async Task ReportAsync(
        string key, ISandbox sandbox, IReadOnlyList<ContextVerifyStages> contexts,
        VerifyResolutionNotes notes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(notes);
        foreach (var context in contexts)
        {
            // No source named, or a source never stamped: there is nothing to compare, and
            // silence is the honest answer. A declaration written by hand is not accused of
            // being stale because nobody hashed it.
            if (context.DerivedFrom is not { Hash: { Length: > 0 } recorded } derivation) continue;

            var current = await digest.ComputeAsync(sandbox, context.Workdir, derivation.Files, ct);
            if (string.Equals(current, recorded, StringComparison.Ordinal)) continue;

            logger.LogWarning(
                "{Key}: context '{Context}' declares verify stages derived from [{Files}]; "
                + "those files now hash {Current}, recorded {Recorded}. Running the "
                + "declaration unchanged.",
                key, context.ContextName, string.Join(", ", derivation.Files), current, recorded);
            notes.DerivationMoved(key, context.ContextName, derivation.Files);
        }
    }
}
