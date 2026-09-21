using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-09-20-4981: the binding the skill catalog LAST resolved to, or <c>null</c> when
/// nothing has resolved yet. A READ, never a resolve — a surface that has to answer while
/// the catalog is unavailable must be able to say what happened instead of triggering the
/// pull that is already failing.
/// <para>
/// Segregated from <see cref="ISkillsCatalogPath"/> on purpose: every reader of that
/// contract wants a root to load files from, and widening it would hand a dozen
/// implementations a member none of them can answer.
/// </para>
/// </summary>
public interface IResolvedCatalogBinding
{
    /// <summary>
    /// The last resolution published by the resolver, or null when the catalog has never
    /// resolved in this process.
    /// </summary>
    CatalogResolution? Current { get; }
}
