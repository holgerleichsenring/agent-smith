namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: one minted look, structured beside the line the model reads. The line is
/// prose; a reader that has to decide what a look PROVES needs its parts — above all whether
/// the tool reached a verdict at all, because a look that could not run proves nothing and
/// still carries an id. Positional so later readers can add fields without breaking callers.
/// </summary>
/// <param name="Id">The id as minted, e.g. <c>R3</c>.</param>
/// <param name="Repository">The repository the look named.</param>
/// <param name="What">What ran, as the line states it.</param>
/// <param name="ExitCode">The exit the tool reported.</param>
/// <param name="Ran">Whether that exit means the tool reached a verdict.</param>
/// <param name="Line">The minted line, id first.</param>
/// <param name="Kind">2026-09-17-042eh: which tool looked (<see cref="EvidenceRecord"/>).</param>
/// <param name="Path">2026-09-17-042eh: the path the look was scoped to, or null.</param>
/// <param name="LinesReturned">2026-09-17-042eh: numbered lines the result carried, or 0.</param>
public sealed record EvidenceLook(
    string Id, string Repository, string What, int ExitCode, bool Ran, string Line,
    string Kind = "", string? Path = null, int LinesReturned = 0);
