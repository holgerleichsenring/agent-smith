using System.Diagnostics.CodeAnalysis;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Global label-to-pipeline mapping. Used as fallback when a project's
/// trigger-block omits its own pipeline_from_label dictionary.
/// </summary>
public sealed class PipelineTriggerMap
{
    public static readonly PipelineTriggerMap Empty = new(new Dictionary<string, string>());

    private readonly IReadOnlyDictionary<string, string> _map;

    public PipelineTriggerMap(IReadOnlyDictionary<string, string> map)
    {
        _map = map;
    }

    public IReadOnlyDictionary<string, string> AsDictionary => _map;

    public bool TryResolve(string label, [NotNullWhen(true)] out string? pipelineName)
        => _map.TryGetValue(label, out pipelineName);
}
