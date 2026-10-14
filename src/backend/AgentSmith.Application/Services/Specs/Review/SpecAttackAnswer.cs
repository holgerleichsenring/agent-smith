using System.Text.Json;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762e: an attack reviewer's answer as it wrote it, before admission.
/// <see cref="Verdict"/> is already one of <see cref="SpecReviewVerdicts"/>.</summary>
internal sealed record SpecAttackAnswer(
    string Verdict, IReadOnlyList<SpecAttackItem> Cuts, IReadOnlyList<SpecAttackItem> Findings);

/// <summary>2026-10-13-8762e: one cut or finding as written. A cut carries its register, quote and
/// replacement; a finding its text; both name where they looked and the id of that look.</summary>
internal sealed record SpecAttackItem(
    string? Register, string? Quote, string? Replacement, string? Text,
    string Repository, string? Path, int Line, string? Cites);

/// <summary>2026-10-13-8762e: the answer's wire shape. A line arrives as a number or a string and a
/// citation as a string or a list, normalised as <see cref="SpecFindingWire"/> does.</summary>
internal sealed record SpecAttackWire(
    string? Verdict, List<SpecAttackItemWire>? Cuts, List<SpecAttackItemWire>? Findings)
{
    public SpecAttackAnswer? ToAnswer() => VerdictOf(Verdict) is { } verdict
        ? new SpecAttackAnswer(verdict, [.. (Cuts ?? []).Select(c => c.ToItem())],
            [.. (Findings ?? []).Select(f => f.ToItem())])
        : null;

    internal static string? VerdictOf(string? text) =>
        string.Join(' ', (text ?? string.Empty).ToUpperInvariant()
            .Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries)) switch
        {
            "BUILD" => SpecReviewVerdicts.Build,
            "BUILD WITH CUTS" => SpecReviewVerdicts.BuildWithCuts,
            "REFUSE" => SpecReviewVerdicts.Refuse,
            _ => null,
        };
}

internal sealed record SpecAttackItemWire(
    string? Register, string? Quote, string? Replacement, string? Text, string? Repository, string? Path,
    JsonElement? Line, JsonElement? Cites)
{
    public SpecAttackItem ToItem() => new(
        Register?.Trim().ToLowerInvariant(), Quote, Replacement, Text, Repository ?? string.Empty, Path,
        SpecFindingWire.LineNumber(Line), SpecFindingWire.CitationText(Cites));
}
