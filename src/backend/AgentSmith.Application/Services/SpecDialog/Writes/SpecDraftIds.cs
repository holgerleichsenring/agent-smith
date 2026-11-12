using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using YamlDotNet.Core;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: the id a written spec takes. A spec written without <c>spec:</c> gets a placeholder id the store
/// mints, unique in the draft; one that states an id is that spec — rewritten when the draft holds it, new otherwise.
/// Filing replaces every placeholder with its series member id.
/// </summary>
public sealed class SpecDraftIds(SeriesIdFactory ids)
{
    /// <summary>The spec's text with its id stated, and the id; or why it cannot be placed.</summary>
    public (string? Yaml, string? Id, string? Error) Place(ProposalDraft draft, string yaml)
    {
        ArgumentNullException.ThrowIfNull(draft);
        IReadOnlyDictionary<string, object?> map;
        try
        {
            map = OutcomeYamlReader.ReadMap(yaml ?? string.Empty);
        }
        catch (YamlException ex)
        {
            return (null, null, $"the spec is not valid YAML: {ex.Message}");
        }
        var taken = draft.Specs.Select(s => s.Draft.SpecId).ToHashSet(StringComparer.Ordinal);
        if (!map.TryGetValue("spec", out var stated)) return Minted(yaml!, taken);
        return stated is string id && RequiresEdgeChecker.SpecIdRegex().IsMatch(id.Trim())
            ? (yaml, id.Trim(), null)
            : (null, null, $"'spec: {stated}' is no spec id — leave 'spec' out to have one minted, or name a spec of "
                + $"this draft to rewrite it ({(taken.Count == 0 ? "the draft holds no spec" : string.Join(", ", taken))})");
    }

    private (string Yaml, string Id, string? Error) Minted(string yaml, IReadOnlySet<string> taken)
    {
        var id = ids.Mint();
        while (taken.Contains(id)) id = ids.Mint();
        return ($"spec: {id}\n{yaml.TrimStart()}", id, null);
    }
}
