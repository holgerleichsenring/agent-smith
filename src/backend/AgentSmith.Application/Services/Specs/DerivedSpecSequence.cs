using System.Text.Json;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the ordered specs of a derivation reply — the executed head kept verbatim, every
/// spec after it built anew with the repository <see cref="DerivedSpecRepos"/> decided for it
/// (2026-10-12-3a36e/f5f7a). Split from <see cref="SpecDerivationParser"/>, which reads the
/// envelope around the specs and refuses a reply whose shape cannot hold them.
/// </summary>
public sealed class DerivedSpecSequence(DerivedSpecBuilder specBuilder, DerivedSpecRepos specRepos)
{
    /// <param name="specElements">Every spec element of the reply, the head's positions included.</param>
    /// <param name="executedHead">Specs that already ran; never more than <paramref name="specElements"/>.</param>
    /// <param name="evidence">The evidence lines a fact line may cite; see <see cref="FactResolver"/>.</param>
    /// <param name="scope">The scope's repository names; empty when none is known.</param>
    /// <param name="taken">2026-10-12-f5f7d: ids (or done/ spec file names) a new spec must not take;
    /// a taken position's letter skips to the next free one.</param>
    /// <param name="shared">2026-10-17-293ba: segments every spec built here carries beside its own;
    /// the executed head is kept as it ran.</param>
    public (IReadOnlyList<DerivedSpec>? Phases, string? Error) Build(
        IReadOnlyList<JsonElement> specElements, IReadOnlyList<DerivedSpec> executedHead, string series,
        string ticketId, IReadOnlyList<TicketSegment> segments, IReadOnlyList<string>? evidence,
        IReadOnlyList<string> scope, IReadOnlyCollection<string>? taken = null, IReadOnlyList<int>? shared = null)
    {
        ArgumentNullException.ThrowIfNull(specElements);
        ArgumentNullException.ThrowIfNull(executedHead);
        var repos = specRepos.Decide([.. specElements.Skip(executedHead.Count)], scope);
        if (repos.Error is not null) return (null, repos.Error);
        var built = new List<DerivedSpec>(executedHead);
        var letter = executedHead.Count;
        IReadOnlyCollection<string> skip = [.. taken ?? [], .. executedHead.Select(p => p.SpecId)];
        for (var i = executedHead.Count; i < specElements.Count; i++)
        {
            var (specId, at) = SeriesIdFactory.NextFree(series, letter, skip);
            letter = at + 1;
            var (spec, error) = specBuilder.Build(
                specElements[i], i, specId, ticketId, segments, built, evidence,
                repos.Repos[i - executedHead.Count], shared);
            if (spec is null) return (null, error);
            built.Add(spec);
        }
        return (built, null);
    }
}
