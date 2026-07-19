using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0349: one entry of the type&lt;-&gt;model map — how a single config doc type reads
/// out of and writes back into the raw config, plus the reference edges it emits.
/// A collection type yields one entry per catalog id; a singleton yields exactly
/// one entry with id 'default'. This is the single place the taxonomy lives.
/// </summary>
internal sealed record ConfigDocDescriptor(
    string Type,
    bool IsSingleton,
    Func<RawAgentSmithConfig, IEnumerable<(string Id, object Value)>> Read,
    Action<RawAgentSmithConfig, string, JsonElement> Write,
    Func<JsonElement, IEnumerable<ConfigDocEdge>>? Edges = null)
{
    public static ConfigDocDescriptor Collection<T>(
        string type,
        Func<RawAgentSmithConfig, IDictionary<string, T>> accessor,
        Func<JsonElement, IEnumerable<ConfigDocEdge>>? edges = null) =>
        new(
            type,
            IsSingleton: false,
            Read: raw => accessor(raw).Select(kv => (kv.Key, (object)kv.Value!)),
            Write: (raw, id, el) => accessor(raw)[id] = el.Deserialize<T>(ConfigDocJson.Options)!,
            Edges: edges);

    public static ConfigDocDescriptor Singleton<T>(
        string type,
        Func<RawAgentSmithConfig, T> getter,
        Action<RawAgentSmithConfig, T> setter) =>
        new(
            type,
            IsSingleton: true,
            Read: raw => [(DefaultId, getter(raw)!)],
            Write: (raw, _, el) => setter(raw, el.Deserialize<T>(ConfigDocJson.Options)!));

    public const string DefaultId = "default";
}
