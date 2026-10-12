using System.Text.Json;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36e: the repositories each phase of a derivation reply names — read off the
/// phase elements, decided by <see cref="SpecReposRule"/> against the scope, and, where the
/// reply stated them, held against the <c>repo/context</c> names the same phase declares.
/// 2026-10-12-f5f7a: in a scope of several repositories that is exactly one per phase.
/// Split from <see cref="SpecDerivationParser"/>, which reads the ordered set.
/// </summary>
public sealed class DerivedPhaseRepos(SpecDerivationEnvelope envelope, SpecReposRule rule)
{
    /// <param name="phases">The phase elements the reply cut anew, in order.</param>
    /// <param name="scope">The scope's repository names; empty when none is known.</param>
    public SpecReposResolution Decide(IReadOnlyList<JsonElement> phases, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(phases);
        var resolution = rule.Resolve([.. phases.Select(envelope.Repos)], scope);
        if (resolution.Error is not null || !resolution.Stated) return resolution;
        for (var i = 0; i < phases.Count; i++)
        {
            var error = rule.ContextDisagreement(resolution.Repos[i], envelope.Contexts(phases[i]), i + 1);
            if (error is not null) return SpecReposResolution.Refused(error);
        }
        return resolution;
    }
}
