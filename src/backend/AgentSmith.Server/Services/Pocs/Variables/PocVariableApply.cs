using AgentSmith.Application.Services.Pocs.Acts;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Pocs.Acts;

namespace AgentSmith.Server.Services.Pocs.Variables;

/// <summary>
/// 2026-11-23-c81ed: a variable change reaches the pod at once — after the poc-variables run succeeded, a PoC whose state
/// apply admits (ready, stopped or failed, with a Revision) gets a poc-apply run, claimed through the run gate's HANDOVER
/// from the variables run the route just awaited (not a fresh claim beside it). A PoC apply does not admit queues nothing
/// and says when the change applies; a deleted one says nothing.
/// </summary>
internal sealed class PocVariableApply(PocRepository pocs, PocActPlan plan, PocActLauncher acts)
{
    public async Task<PocVariablesAnswer> AfterAsync(long pocId, PocVariablesAnswer written, string person, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(written);
        if (await pocs.FindAsync(pocId, ct) is not { } poc || poc.State == PocStates.Deleted) return written;
        if (plan.WhyNot(PocActKinds.Apply, PocActSubjects.Map(poc)) is not null)
            return written with { ApplyNote = $"'{poc.Name}' is {poc.State} and not deployed; the change applies at its next deploy." };
        var (answer, refusal) = await acts.LaunchAsync(poc, PocActKinds.Apply, new PocActRequest(), person, ct, handover: written.RunId);
        return answer is not null
            ? written with { ApplyRunId = answer.RunId }
            : written with { ApplyNote = $"The change was not applied at once ({refusal!.Reason}); it applies after the live run by Apply on "
                + "the detail page, or at the next deploy." };
    }
}
