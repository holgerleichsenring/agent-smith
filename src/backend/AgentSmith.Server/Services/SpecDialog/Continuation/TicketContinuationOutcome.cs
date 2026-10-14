using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: what an approval in a ticket's own conversation did — the line the thread is
/// told, whether it acted (an acted-on proposal is not offered again), and a follow-up's filing for the pane.
/// </summary>
public sealed record TicketContinuationOutcome(
    string Notice, bool Acted, FilingReport? Filing = null, OutcomeProposal? Filed = null);
