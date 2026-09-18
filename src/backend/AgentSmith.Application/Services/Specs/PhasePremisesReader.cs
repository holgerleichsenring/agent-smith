using System.Text.RegularExpressions;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: reads a phase's premises off its draft — the facts and assumptions the
/// draft already parses, and the decisions prose re-parsed from its own yaml.
/// <para>
/// It also builds the CLAIM CORPUS a reported premise must resolve against. A decision entry is
/// a block of prose and a checker quotes a sentence of it, so the entry is offered whole AND
/// sentence by sentence: the word-run match behind <see cref="PhaseQuotableText.IsOneOf"/> wants
/// a quote covering half of what it matches, which one sentence of a long entry never does.
/// </para>
/// </summary>
internal static class PhasePremisesReader
{
    /// <summary>Shortest prose fragment offered as a claim of its own. Below this a sentence
    /// identifies nothing and would admit a finding against almost any wording.</summary>
    private const int MinClaimWords = 4;

    public static PhasePremises Read(PhaseDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var facts = draft.Facts.Where(f => !string.IsNullOrWhiteSpace(f.Claim)).ToList();
        var assumptions = draft.Assumptions.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        var decisions = DecisionsOf(draft.Yaml);
        return new PhasePremises(
            facts, assumptions, string.Join("\n\n", decisions),
            [.. facts.Select(f => f.Claim), .. assumptions, .. decisions.SelectMany(Fragments)]);
    }

    // The schema accepts a decision as a bare line or as a single-keyed map, and the key is
    // sometimes a slug naming the decision rather than the literal "key". Both are prose.
    private static IReadOnlyList<string> DecisionsOf(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return [];
        List<object?>? entries;
        try
        {
            entries = OutcomeYamlReader.GetList(OutcomeYamlReader.ReadMap(yaml), "decisions")?.ToList();
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return [];
        }
        return [.. (entries ?? [])
            .SelectMany(TextOf)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0 && !DerivedPhaseDecision.IsMachineWritten(t))];
    }

    private static IEnumerable<string> TextOf(object? entry) => entry switch
    {
        string line => [line],
        Dictionary<object, object?> map => map
            .Select(e => e.Key.ToString() == "key"
                ? e.Value?.ToString() ?? string.Empty
                : $"{e.Key}: {e.Value}"),
        _ => [],
    };

    /// <summary>The entry whole, plus each sentence long enough to identify itself.</summary>
    private static IEnumerable<string> Fragments(string decision) =>
        [decision, .. Regex.Split(decision, @"(?<=[.;:])\s+|\n")
            .Select(part => part.Trim())
            .Where(part => Regex.Matches(part, @"[\p{L}\p{N}]+").Count >= MinClaimWords)];
}
