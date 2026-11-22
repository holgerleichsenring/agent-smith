using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-22-6fd7a: what a master whose checklist is done and that has not called finish_spec is told. Run 845e
/// answered its report as text twice and was told "these checklist steps are still open" over a done checklist;
/// the lead names the one thing missing — the report and the finish_spec call in the same reply.
/// </summary>
internal static class MasterFinishLead
{
    /// <summary>The lead the verdict and re-engagement nudges open with over a done checklist.</summary>
    internal const string Text =
        "Every checklist step is done, but you have not called finish_spec, so the spec cannot be reported. "
        + "Close now with one reply whose text is your report for the person and which calls finish_spec with the "
        + "real outcome (status: green | no-tests | failed | blocked) and a one-line summary. A reply with text and no "
        + "finish_spec call ends the turn unreported.\n\n";

    /// <summary>The work is done by the master's own checklist and no verdict was recorded.</summary>
    internal static bool Due(ProgressLedger ledger, MasterVerification? verification) =>
        verification is null && !ledger.IsEmpty && !ledger.HasActionablePending;
}
