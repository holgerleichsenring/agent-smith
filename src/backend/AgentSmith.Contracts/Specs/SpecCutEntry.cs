namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-9a59e: one spec of a published cut, as the attempt published it — its place in the set, its
/// file stem, goal and repositories.
/// <para>
/// 2026-11-17-a0b6b: the steps, done list and yaml are no longer carried: the branch is the spec's text and
/// every reader takes it from there. The goal stays as the title of the run's own spec rows. A row stored
/// earlier with the old fields reads back with them ignored.
/// </para>
/// </summary>
public sealed record SpecCutEntry(
    string SpecId,
    int Ordinal,
    string FileStem,
    string Goal,
    IReadOnlyList<string> Repos);
