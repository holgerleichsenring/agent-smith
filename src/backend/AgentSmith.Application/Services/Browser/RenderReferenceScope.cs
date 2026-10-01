using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283df: what one render_reference host renders FOR. A design turn holds its browser
/// under its conversation and resolves an address in its sandbox map; a run has no conversation —
/// its browser is spawned per render and disposed after, inside the pod admission reserved — and
/// resolves an address among the sets it carries.
/// </summary>
/// <param name="Project">The project the browser sandbox's spec is built from.</param>
/// <param name="Conversation">The conversation the browser is held under; null in a run.</param>
/// <param name="Sandboxes">The turn's or run's sandbox map.</param>
/// <param name="Carried">The uploaded websites a run carries; empty in a design turn.</param>
/// <param name="Repos">2026-10-01-283dh: the repositories by name, whose HTML files render by path.</param>
public sealed record RenderReferenceScope(
    ResolvedProject Project,
    string? Conversation,
    IReadOnlyDictionary<string, ISandbox> Sandboxes,
    IReadOnlyList<CarriedReferenceSet> Carried,
    IReadOnlyDictionary<string, ISandbox>? Repos = null)
{
    /// <summary>The repositories by name; empty when the scope names none.</summary>
    public IReadOnlyDictionary<string, ISandbox> RepoSandboxes => Repos ?? new Dictionary<string, ISandbox>();
}
