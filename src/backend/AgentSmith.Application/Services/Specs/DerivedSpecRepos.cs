using System.Text.Json;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36e: the repositories each spec of a derivation reply names — read off the
/// spec elements, decided by <see cref="SpecReposRule"/> against the scope, and, where the
/// reply stated them, held against the <c>repo/context</c> names the same spec declares.
/// 2026-10-12-f5f7a: in a scope of several repositories that is exactly one per spec.
/// Split from <see cref="SpecDerivationParser"/>, which reads the ordered set.
/// </summary>
public sealed class DerivedSpecRepos(SpecDerivationEnvelope envelope, SpecReposRule rule)
{
    /// <param name="specs">The spec elements the reply cut anew, in order.</param>
    /// <param name="scope">The scope's repository names; empty when none is known.</param>
    public SpecReposResolution Decide(IReadOnlyList<JsonElement> specs, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(specs);
        var resolution = rule.Resolve([.. specs.Select(envelope.Repos)], scope);
        if (resolution.Error is not null || !resolution.Stated) return resolution;
        for (var i = 0; i < specs.Count; i++)
        {
            var error = rule.ContextDisagreement(resolution.Repos[i], envelope.Contexts(specs[i]), i + 1);
            if (error is not null) return SpecReposResolution.Refused(error);
        }
        return resolution;
    }
}
