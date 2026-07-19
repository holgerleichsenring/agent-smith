using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0349: drives both directions of the type&lt;-&gt;model map. Decompose splits a raw
/// config into per-entity JSON docs (with derived edges) for the store; Assemble
/// reads all stored rows back into a raw config. The taxonomy is the only place the
/// entity set is enumerated, so the two directions can never drift.
/// </summary>
public sealed class ConfigDocumentAssembler
{
    public IReadOnlyList<DecomposedConfigDoc> Decompose(RawAgentSmithConfig raw)
    {
        var docs = new List<DecomposedConfigDoc>();
        foreach (var descriptor in ConfigDocumentTaxonomy.All)
            foreach (var (id, value) in descriptor.Read(raw))
                docs.Add(ToDoc(descriptor, id, value));
        return docs;
    }

    public RawAgentSmithConfig Assemble(IReadOnlyList<ConfigDocRow> rows)
    {
        var raw = new RawAgentSmithConfig();
        var byType = ConfigDocumentTaxonomy.All.ToDictionary(d => d.Type);
        foreach (var row in rows)
        {
            if (!byType.TryGetValue(row.Type, out var descriptor)) continue;
            using var parsed = JsonDocument.Parse(row.Doc);
            descriptor.Write(raw, row.Id, parsed.RootElement.Clone());
        }
        return raw;
    }

    public IReadOnlyList<ConfigDocEdge> EdgesFor(string type, string doc)
    {
        var descriptor = ConfigDocumentTaxonomy.All.FirstOrDefault(d => d.Type == type);
        if (descriptor?.Edges is null) return [];
        using var parsed = JsonDocument.Parse(doc);
        return descriptor.Edges(parsed.RootElement).ToList();
    }

    private static DecomposedConfigDoc ToDoc(ConfigDocDescriptor descriptor, string id, object value)
    {
        var doc = JsonSerializer.Serialize(value, ConfigDocJson.Options);
        var edges = descriptor.Edges is null
            ? (IReadOnlyList<ConfigDocEdge>)[]
            : EdgesFrom(descriptor, doc);
        return new DecomposedConfigDoc(descriptor.Type, id, doc, edges);
    }

    private static IReadOnlyList<ConfigDocEdge> EdgesFrom(ConfigDocDescriptor descriptor, string doc)
    {
        using var parsed = JsonDocument.Parse(doc);
        return descriptor.Edges!(parsed.RootElement).ToList();
    }
}
