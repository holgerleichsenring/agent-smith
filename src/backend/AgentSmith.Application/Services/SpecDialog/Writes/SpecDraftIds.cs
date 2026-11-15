using System.Text.RegularExpressions;
using YamlDotNet.Core;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: the id a written spec takes. 2026-11-15-06fc: the file name gives it — <c>specs/export-queue.yaml</c>
/// is spec <c>export-queue</c> — so a spec rewritten at its path keeps its id. A draft names its specs by a label: a
/// lower-case name that is not a spec id, so it can never be read as one; filing mints each spec's series id. A text
/// without <c>spec:</c> has it stated; one that states another name is refused. A spec id already in a draft (a
/// proposal written before labels) is still accepted.
/// </summary>
public sealed partial class SpecDraftIds
{
    /// <summary>The id a label is validated under: the schema knows spec ids, and a label is one until filing.</summary>
    private const string SchemaStandIn = "2000-01-01-0000";

    /// <summary>Whether a draft spec's name is a label rather than a spec id.</summary>
    public static bool IsLabel(string id) => LabelShape().IsMatch(id) && !RequiresEdgeChecker.SpecIdRegex().IsMatch(id);

    /// <summary>The spec's text with its name stated, or why it cannot be placed.</summary>
    public (string? Yaml, string? Error) Place(string id, string yaml)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!IsLabel(id) && !RequiresEdgeChecker.SpecIdRegex().IsMatch(id))
            return (null, $"'{id}' is no spec name — a spec's file name is a short label of lower-case letters, digits "
                + "and '-', such as server-foundation");
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
                + "name its file gives");
    }

    /// <summary>The placed text as the schema reads it: a label's <c>spec:</c> line stands in as a spec id.</summary>
    public string ForSchema(string id, string placed) => !IsLabel(id) ? placed
        : string.Join('\n', placed.Split('\n').Select(line => line.StartsWith("spec:", StringComparison.Ordinal)
            ? $"spec: {SchemaStandIn}" : line));

    [GeneratedRegex("^[a-z][a-z0-9-]{0,59}$")]
    private static partial Regex LabelShape();
}
