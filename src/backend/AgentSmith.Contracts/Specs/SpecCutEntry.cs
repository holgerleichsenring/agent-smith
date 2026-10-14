using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-9a59e: one spec of a published cut, as the attempt published it — its place in the
/// set, its file stem, goal, repositories, steps and done list, and the yaml it was rendered as. The
/// structured fields are what readers use; the yaml is the text a later comparison keys on.
/// </summary>
public sealed record SpecCutEntry(
    string SpecId,
    int Ordinal,
    string FileStem,
    string Goal,
    IReadOnlyList<string> Repos,
    IReadOnlyList<SpecStep> Steps,
    IReadOnlyList<string> Done,
    string Yaml);
