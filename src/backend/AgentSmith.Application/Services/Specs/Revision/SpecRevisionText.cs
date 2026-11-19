using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: reads the text the coding master revised its current spec to. It is checked against the spec
/// schema as filing checks a spec (<see cref="ISpecDraftValidator"/>), may not move what makes it this spec in its set
/// (<see cref="SpecIdentityRule"/>: spec, requires, repos, applies_to), and must differ from the current text. A
/// refusal is the exact reason; the draft keeps the repositories the current one was placed in.
/// </summary>
public sealed class SpecRevisionText(ISpecDraftValidator validator, SpecDraftReader reader)
{
    /// <param name="isStandIn">2026-11-19-6b0ba: the current spec is the ticket's stand-in, which may gain applies_to.</param>
    public (SpecDraft? Draft, string? Refusal) Read(SpecDraft current, string? yaml, bool isStandIn = false)
    {
        ArgumentNullException.ThrowIfNull(current);
        var text = yaml ?? string.Empty;
        var outcome = text.Contains("```", StringComparison.Ordinal) ? validator.Validate(text) : validator.ValidateYaml(text);
        if (outcome is not SpecDraftValid valid)
            return (null, outcome is SpecDraftInvalid invalid ? $"the spec is invalid: {invalid.Error}" : "spec_yaml holds no spec");
        var draft = reader.Read(valid.Yaml) with { Repos = current.Repos };
        if (!SpecIdentityRule.Holds(current, draft, isStandIn))
            return (null, $"a revision keeps spec ({current.SpecId}), requires, repos and applies_to as they are; "
                + "a change to those is a different spec, which a new spec in the series carries");
        return Unchanged(current.Yaml, draft.Yaml) ? (null, "the text is the spec as it stands; nothing to revise") : (draft, null);
    }

    private static bool Unchanged(string current, string revised)
    {
        var before = OutcomeYamlReader.ReadMap(current);
        var after = OutcomeYamlReader.ReadMap(revised);
        return before.Count == after.Count
            && before.All(entry => after.TryGetValue(entry.Key, out var other) && SpecRevisionEdit.Equal(entry.Value, other));
    }
}
