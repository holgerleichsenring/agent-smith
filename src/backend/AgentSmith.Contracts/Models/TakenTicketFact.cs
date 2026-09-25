namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-25-b4d9: one ticket this framework took up and has not finished. Written when a
/// claim is granted, removed when the ticket reaches a terminal state — so it OUTLIVES the
/// reaper, which deletes the lease three minutes after a crash.
/// <para>
/// It carries everything a re-enqueue needs, the PIPELINE included. The lease has no pipeline
/// column, and the reconciler used to recover it from the ticket's labels: a board an operator
/// may edit was the durable record and the database the transient one, which is backwards.
/// </para>
/// </summary>
public sealed record TakenTicketFact(
    string Project,
    string TicketId,
    string Platform,
    string Pipeline);
