using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-05-862eb: outcome_format — the design master reads the format of the block it is about to draft from the
/// catalog's references, so the formats ride the turn that proposes rather than every turn's system prompt. An epic
/// gets the spec format first, since each of its children is a spec. A catalog that ships no such reference keeps
/// the formats in the master's own instructions, and the answer says so.
/// </summary>
public sealed class OutcomeFormatToolHost(ISkillReferenceSource references) : IToolHost
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
        "Return the format of an outcome block before you draft or re-draft it — after a validation error, a "
        + "review's cuts or an operator's edit note too. It is not a source read.")]
    private string OutcomeFormat(
        [Description("spec, bug, epic, tickets, comment or document.")] string kind)
    {
        var wanted = (kind ?? string.Empty).Trim();
        var format = Formats.FirstOrDefault(f => string.Equals(f.Kind, wanted, StringComparison.OrdinalIgnoreCase));
        if (format.Slugs is null)
            return $"There is no format for '{wanted}'. The kinds are: {string.Join(", ", Formats.Select(f => f.Kind))}.";
        var bodies = format.Slugs.Select(references.TryRead).ToList();
        return bodies.Any(body => body is null)
            ? InOwnInstructions
            : string.Join("\n\n", bodies.Select(body => body!.TrimEnd()));
    }
}
