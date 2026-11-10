using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-05-862eb: outcome_format — the design master reads the format of the block it is about to draft from the
/// catalog's references, so the formats ride the turn that proposes rather than every turn's system prompt. An epic
/// gets the spec format first, since each of its children is a spec. A catalog that ships no such reference keeps
/// the formats in the master's own instructions, and the answer says so. 2026-11-10-14a3f: a format is returned once
/// per turn (one host serves one turn); asked again, the tool says it stands above — conversation 43 carried the
/// 12000-character epic format twice in one turn. 2026-11-10-85e2: unless the conversation was folded since — a fold
/// summarizes what stood above, so the format is returned again.
/// </summary>
public sealed class OutcomeFormatToolHost(ISkillReferenceSource references, TurnFolds? folds = null) : IToolHost
{
    public const string Name = "outcome_format";

    internal const string InOwnInstructions =
        "The outcome formats are in your own instructions: draft the block exactly as they describe it.";

    private static readonly (string Kind, string[] Slugs)[] Formats =
    [
        ("spec", ["design-outcome-spec"]),
        ("bug", ["design-outcome-bug"]),
        ("epic", ["design-outcome-spec", "design-outcome-epic"]),
        ("tickets", ["design-outcome-tickets"]),
        ("comment", ["design-outcome-comment"]),
        ("document", ["writing-documents"]),
    ];

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(OutcomeFormat, name: Name)];
    }

    [Description(
        "Return the format of an outcome block before you draft it. A format returned once stands in this turn, so a "
        + "re-draft writes to the one above. It is not a source read.")]
    private string OutcomeFormat(
        [Description("spec, bug, epic, tickets, comment or document.")] string kind)
    {
        var wanted = (kind ?? string.Empty).Trim();
        var format = Formats.FirstOrDefault(f => string.Equals(f.Kind, wanted, StringComparison.OrdinalIgnoreCase));
        if (format.Slugs is null)
            return $"There is no format for '{wanted}'. The kinds are: {string.Join(", ", Formats.Select(f => f.Kind))}.";
        var bodies = format.Slugs.Select(slug => (Slug: slug, Body: references.TryRead(slug))).ToList();
        if (bodies.Any(b => b.Body is null)) return InOwnInstructions;
        var now = folds?.Count ?? 0;
        var fresh = bodies.Where(b => !_given.TryGetValue(b.Slug, out var at) || at != now).ToList();
        foreach (var b in fresh) _given[b.Slug] = now;
        return fresh.Count > 0
            ? string.Join("\n\n", fresh.Select(b => b.Body!.TrimEnd()))
            : $"The {wanted} format was returned earlier in this turn and stands above; write the block to it.";
    }

    private readonly Dictionary<string, int> _given = new(StringComparer.Ordinal);
}
