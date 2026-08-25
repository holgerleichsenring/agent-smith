namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-08-25-a508: a run that stopped on a question — status waiting_for_input, no finish,
/// no cancel. Carries only what naming it to an operator needs; whether it holds an ANSWERABLE
/// question is a separate question, answered by the checkpoint store.
/// </summary>
public sealed record ParkedRun(string RunId, string Project, string TicketId);
