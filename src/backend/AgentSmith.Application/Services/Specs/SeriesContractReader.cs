using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762j: reads the optional <c>contract</c> block of a derivation reply or an epic outcome into a
/// contract keyed by spec ids, and checks it with <see cref="SeriesContractCheck"/>. In a derivation reply a member
/// is its 1-based position in THAT reply, or the id of an executed spec, whose position is fixed: every later id
/// is re-assigned by position on each parse, so a copied id of an unexecuted spec could name another spec after a
/// merge or split. In an epic block a member is a child's id or its position.
/// </summary>
public sealed class SeriesContractReader(SeriesContractCheck check)
{
    public const string Key = "contract";

    public SeriesContractRead FromReply(
        JsonElement root, IReadOnlyList<DerivedSpec> built, IReadOnlyList<DerivedSpec> executedHead)
    {
        ArgumentNullException.ThrowIfNull(built);
        if (!SpecJsonReader.TryGet(root, Key, out var block) || block.ValueKind != JsonValueKind.Object)
            return SeriesContractRead.None;
        var head = (executedHead ?? []).Select(s => s.SpecId).ToHashSet(StringComparer.Ordinal);
        var elements = new SeriesContractElements([.. built.Select(s => s.SpecId)], id => head.Contains(id) ? id : null,
            "name a spec by its position in your reply; only an executed spec keeps its id");
        return Checked(elements, block, [.. built.Select(s => (s.SpecId, s.Draft.Repos))]);
    }

    public SeriesContractRead FromOutcome(IReadOnlyDictionary<string, object?> map, IReadOnlyList<SpecDraft> children)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(children);
        if (!map.TryGetValue(Key, out var node) || node is null) return SeriesContractRead.None;
        if (OutcomeYamlReader.GetMap(map, Key) is not { } block)
            return new(null, "the series contract cannot be read: 'contract' is not a mapping");
        var ids = children.Select(c => c.SpecId).ToList();
        var elements = new SeriesContractElements(ids, id => ids.Contains(id, StringComparer.Ordinal) ? id : null,
            "name a child by its id or its position");
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(block.ToDictionary(e => e.Key, e => Plain(e.Value))));
        return Checked(elements, json.RootElement, [.. children.Select(c => (c.SpecId, c.Repos))]);
    }

    private SeriesContractRead Checked(
        SeriesContractElements elements, JsonElement block, IReadOnlyList<(string Id, IReadOnlyList<string> Repos)> members)
    {
        var contract = elements.Read(block);
        if (elements.Problems.Count > 0)
            return new(null, "the series contract cannot be read: " + string.Join("; ", elements.Problems));
        if (contract.IsEmpty) return SeriesContractRead.None;
        return check.Problems(contract, members) is { } problem ? new(null, problem) : new(contract, null);
    }

    // The untyped YAML read keys its maps by object; JSON wants string keys, every scalar stays a string.
    private static object? Plain(object? node) => node switch
    {
        Dictionary<object, object?> map => map.ToDictionary(e => e.Key.ToString() ?? string.Empty, e => Plain(e.Value)),
        List<object?> list => list.Select(Plain).ToList(),
        _ => node,
    };
}
