using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-11-23-31dca: a dependent's stack per repo — <see cref="Bases"/> maps each repo to the unlanded prerequisite whose
/// branch it is cut from; <see cref="Merges"/> names the merges that leave a base in every repo where two independent
/// prerequisites worked, empty when there is none.
/// </summary>
public sealed record RepoStack(IReadOnlyDictionary<string, string> Bases, IReadOnlyList<TicketRequirementEdge> Merges);
