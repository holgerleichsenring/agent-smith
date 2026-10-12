namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36e: what <see cref="SpecReposRule"/> decided for a cut — the repositories
/// each spec names, in the order the specs were given, or the reason the cut is refused.
/// </summary>
/// <param name="Repos">One entry per spec; empty when refused.</param>
/// <param name="Stated">True when the reply stated the field, which arms the contexts check.</param>
/// <param name="Error">Why the reply is asked again; null when it stands.</param>
/// <param name="NeedsSplit">2026-10-12-f5f7a: true when the refusal is a spec naming several
/// repositories, so a caller can say how ITS surface splits one (an epic in the dialog).</param>
public sealed record SpecReposResolution(
    IReadOnlyList<IReadOnlyList<string>> Repos, bool Stated, string? Error, bool NeedsSplit = false)
{
    public static SpecReposResolution Refused(string error) => new([], false, error);

    public static SpecReposResolution SplitNeeded(string error) => new([], false, error, NeedsSplit: true);
}
