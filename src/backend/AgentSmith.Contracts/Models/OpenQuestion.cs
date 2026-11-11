namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-10-d854a: a question only the operator can answer, laid on a design turn's draft for its card: the question,
/// the master's proposal, and the ids of the specs it touches. The same question stays in the reply, which every surface shows.
/// </summary>
public sealed record OpenQuestion(string Question, string Proposal, IReadOnlyList<string> SpecIds);
