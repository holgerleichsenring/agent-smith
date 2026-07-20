using System.Text.Json;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0353: the generic read/normalize surface for the global SETTINGS singletons —
/// the taxonomy's singleton docs surfaced as editable typed forms in the studio.
/// Keyed by the taxonomy type, so one code path serves all of them and the two
/// directions can never drift from the entity map. <c>persistence</c> is excluded:
/// it is bootstrap-only (read from file/env before the DB) and never editable.
/// </summary>
internal static class ConfigSettingsAccess
{
    /// <summary>The singleton types exposed as editable settings (every taxonomy singleton minus bootstrap-only persistence).</summary>
    public static readonly IReadOnlyList<string> Types = ConfigDocumentTaxonomy.All
        .Where(d => d.IsSingleton && d.Type != ConfigDocTypes.Persistence)
        .Select(d => d.Type)
        .ToList();

    /// <summary>Read one settings singleton's assembled value as its typed model (serialized camelCase on the wire).</summary>
    public static object Read(RawAgentSmithConfig raw, string type) =>
        Descriptor(type).Read(raw).Single().Value;

    /// <summary>
    /// Bind an incoming settings doc to its typed model (rejecting a malformed shape
    /// as a <see cref="ConfigurationException"/> — a client 400, never a 500) and
    /// return the normalized value to persist through the doc store.
    /// </summary>
    public static object Normalize(string type, JsonElement doc)
    {
        var descriptor = Descriptor(type);
        var scratch = new RawAgentSmithConfig();
        try
        {
            descriptor.Write(scratch, ConfigDocDescriptor.DefaultId, doc);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            throw new ConfigurationException($"Invalid '{type}' settings document: {ex.Message}");
        }
        return descriptor.Read(scratch).Single().Value;
    }

    private static ConfigDocDescriptor Descriptor(string type) =>
        type != ConfigDocTypes.Persistence
            ? ConfigDocumentTaxonomy.All.FirstOrDefault(d => d.IsSingleton && d.Type == type)
                ?? throw new ConfigurationException($"Unknown settings type '{type}'.")
            : throw new ConfigurationException($"Settings type '{type}' is not editable.");
}
