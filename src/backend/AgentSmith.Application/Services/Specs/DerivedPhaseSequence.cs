using System.Text.Json;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the ordered phases of a derivation reply — the executed head kept verbatim, every
/// phase after it built anew with the repository <see cref="DerivedPhaseRepos"/> decided for it
/// (2026-10-12-3a36e/f5f7a). Split from <see cref="SpecDerivationParser"/>, which reads the
/// envelope around the phases and refuses a reply whose shape cannot hold them.
/// </summary>
public sealed class DerivedPhaseSequence(DerivedPhaseBuilder phaseBuilder, DerivedPhaseRepos phaseRepos)
{
    /// <param name="phaseElements">Every phase element of the reply, the head's positions included.</param>
    /// <param name="executedHead">Phases that already ran; never more than <paramref name="phaseElements"/>.</param>
    /// <param name="evidence">The evidence lines a fact line may cite; see <see cref="FactResolver"/>.</param>
    /// <param name="scope">The scope's repository names; empty when none is known.</param>
    public (IReadOnlyList<SpecPhase>? Phases, string? Error) Build(
        IReadOnlyList<JsonElement> phaseElements, IReadOnlyList<SpecPhase> executedHead, string series,
        string ticketId, IReadOnlyList<TicketSegment> segments, IReadOnlyList<string>? evidence,
        IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(phaseElements);
        ArgumentNullException.ThrowIfNull(executedHead);
        var repos = phaseRepos.Decide([.. phaseElements.Skip(executedHead.Count)], scope);
        if (repos.Error is not null) return (null, repos.Error);
        var built = new List<SpecPhase>(executedHead);
        for (var i = executedHead.Count; i < phaseElements.Count; i++)
        {
            var (phase, error) = phaseBuilder.Build(
                phaseElements[i], i, series, ticketId, segments, built, evidence,
                repos.Repos[i - executedHead.Count]);
            if (phase is null) return (null, error);
            built.Add(phase);
        }
        return (built, null);
    }
}
