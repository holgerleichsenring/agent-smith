using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using YamlDotNet.Core;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// 2026-08-26-364f: read-modify-write for context.yaml, through the shared
/// <see cref="ContextYamlBuilders"/> configuration — the same generic-map round trip
/// <see cref="ContextYamlRegistryAuthCodec"/> already uses for its one section,
/// applied to the write that states most of the file.
/// <para>
/// The cost is stated rather than discovered: a generic-map round trip loses comments
/// — the <c># yaml-language-server:</c> header that makes an editor validate the file
/// included — and normalises flow style. That is smaller than deleting whole sections,
/// which is what the whole-file write did to state, methodology, integrations, data
/// and decisions every time a context was rewritten.
/// </para>
/// </summary>
public sealed class ContextYamlSectionUpsert(ContextYamlBuilders builders) : IContextYamlSectionUpsert
{
    public ContextYamlUpsertResult Upsert(string? existingYaml, string documentYaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentYaml);
        if (!TryReadMap(existingYaml, out var merged, out var parseError))
            return ContextYamlUpsertResult.Error(parseError!);

        // The keys the typed document CARRIES are exactly the top-level keys of its own
        // YAML, so a section it omits cannot be named here — and the absence of a key in
        // a write is not an instruction to delete it (the p0392a rule for the config
        // store, applied to the file this product tells its users to keep).
        if (!TryReadMap(documentYaml, out var written, out var documentError))
            return ContextYamlUpsertResult.Error(documentError!);
        foreach (var (key, value) in written!) merged![key] = value;

        return ContextYamlUpsertResult.Ok(builders.Serializer.Serialize(merged!));
    }

    private bool TryReadMap(string? yaml, out Dictionary<object, object?>? map, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(yaml))
        {
            map = new Dictionary<object, object?>();
            return true;
        }
        try
        {
            map = builders.Deserializer.Deserialize<Dictionary<object, object?>>(yaml)
                  ?? new Dictionary<object, object?>();
            return true;
        }
        catch (YamlException ex)
        {
            map = null;
            error = Describe(ex);
            return false;
        }
    }

    private static string Describe(YamlException ex) =>
        ex.Start.Line > 0
            ? $"(Line: {ex.Start.Line}, Col: {ex.Start.Column}) {ex.Message}"
            : ex.Message;
}
