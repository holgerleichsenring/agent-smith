using AgentSmith.Contracts.Pipeline;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// Resolves IStepDataFlow declarations registered as DI singletons. Each
/// implementation reports its preset name; the resolver indexes by name once
/// at construction time. O(1) lookup; null when the preset has no declaration.
/// </summary>
public sealed class StepDataFlowResolver : IStepDataFlowResolver
{
    private readonly Dictionary<string, IStepDataFlow> _byName;

    public StepDataFlowResolver(IEnumerable<IStepDataFlow> declarations)
    {
        _byName = declarations.ToDictionary(
            d => d.PresetName, StringComparer.OrdinalIgnoreCase);
    }

    public IStepDataFlow? Resolve(string presetName)
        => _byName.TryGetValue(presetName, out var flow) ? flow : null;
}
