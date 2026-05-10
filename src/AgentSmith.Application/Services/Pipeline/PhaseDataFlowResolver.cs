using AgentSmith.Contracts.Pipeline;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// Resolves IPhaseDataFlow declarations registered as DI singletons. Each
/// implementation reports its preset name; the resolver indexes by name once
/// at construction time. O(1) lookup; null when the preset has no declaration.
/// </summary>
public sealed class PhaseDataFlowResolver : IPhaseDataFlowResolver
{
    private readonly Dictionary<string, IPhaseDataFlow> _byName;

    public PhaseDataFlowResolver(IEnumerable<IPhaseDataFlow> declarations)
    {
        _byName = declarations.ToDictionary(
            d => d.PresetName, StringComparer.OrdinalIgnoreCase);
    }

    public IPhaseDataFlow? Resolve(string presetName)
        => _byName.TryGetValue(presetName, out var flow) ? flow : null;
}
