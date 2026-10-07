using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>2026-10-06-03c7e: one executed spec to record — the repository carrying its series,
/// the series as the run holds it, the spec, the done file's text and its index line.</summary>
public sealed record SpecDoneRecord(
    RepoConnection Carrier, SpecSet Set, SpecPhase Phase, string Body, string IndexLine);
