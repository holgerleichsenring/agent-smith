using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0460: the account a spec gives of itself BEFORE it runs, not only after.
/// <para>
/// A run parks or dies mid-sequence with its work committed on the ticket branch, the
/// operator re-triggers, and the new run opens spec a as if nothing had happened —
/// spending a master pass, real money and often an operator question on work the branch
/// already carries. On ticket 19213 the branch held zero legacy references in both
/// repositories and spec a still asked whether it was allowed to change code.
/// </para>
/// <para>
/// The question is the one <see cref="SpecDeliveryAccounting"/> already answers at the END of a
/// spec — are these criteria satisfied by this branch — asked at the start instead. The
/// answer is always the same and therefore must not be a question put to the operator.
/// </para>
/// </summary>
public sealed class SpecEntryAccount(
    DeliveryDiff deliveryDiff,
    SpecDeliveryAccounting accounting,
    SandboxTargets sandboxTargets,
    ILogger<SpecEntryAccount> logger)
{
    /// <summary>
    /// The accounts for the spec about to be entered — empty when the question could not
    /// be asked cheaply and honestly, which leaves the spec running exactly as before.
    /// </summary>
    public async Task<IReadOnlyList<SpecAccount>> TakeAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var criteria = AcceptanceCriteria.For(pipeline);
        if (criteria.Count == 0) return [];
        if (!sandboxTargets.TryResolve(pipeline, out var sandboxes, out _)) return [];
        if (!await CarriesDeliveryAsync(sandboxes, DeliveryBasis.For(pipeline), cancellationToken)) return [];

        // The branch is the ONLY evidence at entry. No command of THIS spec has run, and
        // the previous spec's commands are another spec's evidence — the p0444 leak. A
        // criterion that can only be closed by a command is therefore outstanding here,
        // and the spec runs: an entry account fails towards doing the work.
        return await accounting.TakeAsync(pipeline, sandboxes, [], cancellationToken);
    }

    /// <summary>
    /// Does the branch carry anything a criterion could be satisfied BY? A fresh branch
    /// answers no, and answering it with a git diff rather than a model call is what keeps
    /// the common case free: an account over an empty diff can only say "nothing is
    /// satisfied", which is what the spec was about to do anyway.
    /// <para>
    /// A diff that could not be taken is not an empty one. Unknown means run the spec.
    /// </para>
    /// </summary>
    private async Task<bool> CarriesDeliveryAsync(
        IReadOnlyDictionary<string, ISandbox> sandboxes, DeliveryBasis basis,
        CancellationToken cancellationToken)
    {
        foreach (var (key, sandbox) in sandboxes)
        {
            var diff = await deliveryDiff.ForBranchAsync(sandbox, basis, cancellationToken);
            if (diff.Failed)
            {
                logger.LogInformation(
                    "{Key}: no delivery diff could be taken ({Basis}) — this spec is worked, "
                    + "not accounted for at entry", key, diff.Basis);
                return false;
            }
            if (DeliveryDiff.CarriesSource(diff.Text)) return true;
        }

        logger.LogInformation(
            "The branch carries no source yet — entering this spec costs no account");
        return false;
    }
}
