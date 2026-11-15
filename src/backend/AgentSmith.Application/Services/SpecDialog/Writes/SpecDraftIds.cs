using AgentSmith.Contracts.Models;
using YamlDotNet.Core;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: the id a written spec takes. 2026-11-15-06fc: the file name gives it — <c>specs/p0001.yaml</c> is
/// spec <c>p0001</c> — so a spec rewritten at its path keeps its id. A text without <c>spec:</c> has it stated; one that
/// states another id is refused. Ids are placeholders: filing replaces each with its series member id.
/// </summary>
public sealed class SpecDraftIds
{
    /// <summary>The spec's text with its id stated, or why it cannot be placed.</summary>
    public (string? Yaml, string? Error) Place(string id, string yaml)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!RequiresEdgeChecker.SpecIdRegex().IsMatch(id))
            return (null, $"'{id}' is no spec id — a spec's file name is its id, such as p0001 or p0002-export");
        IReadOnlyDictionary<string, object?> map;
        try
        {
            map = OutcomeYamlReader.ReadMap(yaml ?? string.Empty);
        }
        catch (YamlException ex)
        {
            return (null, $"the spec is not valid YAML: {ex.Message}");
        }
        if (!map.TryGetValue("spec", out var stated)) return ($"spec: {id}\n{yaml!.TrimStart()}", null);
        return stated is string named && named.Trim() == id
            ? (yaml, null)
            : (null, $"the file names spec '{id}' and its text states 'spec: {stated}' — leave 'spec' out, or state the "
                + "id its file name gives");
    }
}
