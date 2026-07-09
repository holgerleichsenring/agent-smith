using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315e: parses a `kind: epic` outcome block — a parent phase plus ordered
/// child phases — validating the parent and EVERY child against the
/// phase-spec schema (each child is a filable phase in its own right) and
/// the requires: edges for consistency before the shape is proposed.
/// </summary>
public sealed class EpicOutcomeParser(
    ISpecDraftValidator draftValidator,
    PhaseDraftReader draftReader,
    RequiresEdgeChecker edgeChecker)
{
    public OutcomeResolution Parse(IReadOnlyDictionary<string, object?> map)
    {
        if (OutcomeYamlReader.GetMap(map, "parent") is not { } parentMap)
            return new OutcomeInvalid("epic outcome is missing 'parent' (a phase-spec mapping)");
        var childMaps = OutcomeYamlReader.GetList(map, "children");
        if (childMaps is null || childMaps.Count < 2)
            return new OutcomeInvalid(
                "epic outcome needs 'children' with at least two phase-spec entries — "
                + "a single slice is just a phase; emit the bare ```yaml draft instead");

        var (parent, parentError) = ReadDraft(parentMap, "parent");
        if (parent is null) return new OutcomeInvalid(parentError!);

        var children = new List<PhaseDraft>(childMaps.Count);
        for (var i = 0; i < childMaps.Count; i++)
        {
            if (childMaps[i] is not Dictionary<object, object?> childNode)
                return new OutcomeInvalid($"epic child #{i + 1} is not a phase-spec mapping");
            var (child, error) = ReadDraft(childNode, $"child #{i + 1}");
            if (child is null) return new OutcomeInvalid(error!);
            children.Add(child);
        }

        var edgeError = edgeChecker.Check(parent, children);
        return edgeError is null
            ? new OutcomeResolved(new EpicOutcome(parent, children))
            : new OutcomeInvalid(edgeError);
    }

    private (PhaseDraft? Draft, string? Error) ReadDraft(object node, string role)
    {
        var yaml = OutcomeYamlReader.ToYaml(node);
        return draftValidator.ValidateYaml(yaml) switch
        {
            SpecDraftValid valid => (draftReader.Read(valid.Yaml), null),
            SpecDraftInvalid invalid => (null, $"epic {role}: {invalid.Error}"),
            _ => (null, $"epic {role}: the phase-spec entry is empty"),
        };
    }
}
