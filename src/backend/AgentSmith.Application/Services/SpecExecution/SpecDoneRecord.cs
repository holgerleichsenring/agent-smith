using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>2026-10-06-03c7e: one executed spec to record — the repository carrying its series,
/// the series as the run holds it, the spec, the done file's text and its index line.
/// 2026-10-12-f5f7d: <see cref="Carrier"/> is the spec's OWN repository, and
/// <see cref="MultiRepo"/> says the run holds several — where no run-wide sandbox stands in for it.</summary>
public sealed record SpecDoneRecord(
    RepoConnection Carrier, SpecSet Set, DerivedSpec Spec, string Body, string IndexLine, bool MultiRepo = false);
