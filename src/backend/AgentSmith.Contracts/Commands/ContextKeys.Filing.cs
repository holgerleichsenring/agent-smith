namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-5701b: what the server seeds into a 'file' run and what its steps hand each other.</summary>
public static partial class ContextKeys
{
    /// <summary><c>FilingRequest</c> — the approval to file: conversation, principal, scope, the operator's words,
    /// the proposal and whether its approver may start runs. Seeded by the server.</summary>
    public const string FilingRequest = "FilingRequest";

    /// <summary><c>FilingReportSlot</c> — the mutable holder FileTickets fills with what the filing did; the server
    /// reads it back after the run.</summary>
    public const string FilingReport = "FilingReport";

    /// <summary><c>FilingPlan</c> — PlanFiling's ordered tickets, read by FileTickets.</summary>
    public const string FilingPlan = "FilingPlan";
}
