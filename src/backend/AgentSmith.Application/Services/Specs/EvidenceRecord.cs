namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: what a caller tells the evidence about the look it just took.
/// <para>
/// Until now a look was remembered as a repository and one prose string — "read src/Api.csproj"
/// — which is all a MINTED LINE needs and not enough for an admission rule. A code finding
/// rests on the reviewer's own read of exactly that repository and that path, with the line
/// inside what the read returned; none of those three is recoverable from a sentence. The
/// line text is still built from <paramref name="What"/> and is unchanged.
/// </para>
/// </summary>
/// <param name="Repository">The repository the look named.</param>
/// <param name="Kind">Which tool looked — one of the constants below.</param>
/// <param name="What">What ran, as the minted line states it.</param>
/// <param name="ExitCode">The exit the tool reported.</param>
/// <param name="Ran">Whether that exit means the tool reached a verdict.</param>
/// <param name="Path">The path a read or a search was scoped to, or null.</param>
/// <param name="LinesReturned">How many numbered lines the result carried, for a read that
/// numbers them; 0 for every other kind, which is what makes a line citation uncheckable
/// against them.</param>
public sealed record EvidenceRecord(
    string Repository,
    string Kind,
    string What,
    int ExitCode,
    bool Ran,
    string? Path = null,
    int LinesReturned = 0)
{
    public const string Read = "read";
    public const string Search = "search";
    public const string Audit = "audit";
    public const string TemplateProof = "template proof";

    /// <summary>2026-09-20-9c74: one verify stage the repository DECLARED, run by label. Its
    /// own kind because the kinds are a closed set and a holder filters on them — a stage run
    /// is not a read, and admitting it as one would let a line citation be checked against it.</summary>
    public const string VerifyStage = "verify stage";
}
