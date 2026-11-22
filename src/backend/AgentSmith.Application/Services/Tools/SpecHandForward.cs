using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-13-8762m: hand_forward — the build note the master leaves for the next spec of its series:
/// what it renamed, moved, persisted and deviated from. The record step writes it into the spec's record, the only
/// place it is kept. 2026-11-22-96c8a: <see cref="RecordReadAsync"/> merges per field — a read replaces only the
/// fields it names — and hands <see cref="HandForward"/> the merged note.
/// 2026-10-25-9324b: and which memories the spec acted on, each checked against its store and published, so a
/// memory's standing can be traced to the work that used it. 2026-11-10-fcd7c: a project memory is relied on as
/// '@project/slug' where the composition keeps one. 2026-11-22-96c8a: no tool any more — the master names these in
/// its report and <see cref="RecordReadAsync"/> writes what the report's read admits; this stays the one writer.
/// </summary>
public sealed partial class SpecHandForward(
    PipelineContext pipeline, string specId, MasterMemoryUse? memory = null, ILogger? logger = null)
{
    public const int MaxEntries = 20;
    public const int MaxEntryChars = 300;
    public const string Arrow = " -> ";

    public async Task<string> HandForward(
        [Description("Renamed names, each '<old> -> <new>'.")] string[]? renamed = null,
        [Description("Moved files or members, each '<old> -> <new>'.")] string[]? moved = null,
        [Description("What this spec persisted that a later spec reads.")] string[]? persisted = null,
        [Description("Where this spec deviated from its text, and why.")] string[]? deviations = null,
        [Description("The stored memories this spec acted on, each 'slug' or 'repository/slug'.")] string[]? relied_on = null,
        CancellationToken ct = default)
    {
        var lists = new (string Name, IReadOnlyList<string> Entries, bool Arrowed)[]
        {
            ("renamed", Clean(renamed), true), ("moved", Clean(moved), true),
            ("persisted", Clean(persisted), false), ("deviations", Clean(deviations), false),
        };
        var refusals = lists.SelectMany(Refusals).ToList();
        // 2026-11-22-96c8a: no relied_on named, no reliance recorded — an empty list never replaces an earlier one.
        var relied = memory is null || relied_on is not { Length: > 0 } ? null : await memory.ResolveAsync(Clean(relied_on), ct);
        refusals.AddRange(relied?.Problems.Select(p => $"relied_on {p}") ?? []);
        if (refusals.Count > 0)
            return $"Error: the build note was refused, nothing was recorded: {string.Join("; ", refusals)}.";

        var notes = pipeline.TryGet<IReadOnlyDictionary<string, SpecBuildNote>>(ContextKeys.SpecBuildNotes, out var held)
            && held is not null ? new Dictionary<string, SpecBuildNote>(held, StringComparer.Ordinal)
            : new Dictionary<string, SpecBuildNote>(StringComparer.Ordinal);
        var note = notes[specId] = new SpecBuildNote(specId, lists[0].Entries, lists[1].Entries, lists[2].Entries, lists[3].Entries);
        pipeline.Set<IReadOnlyDictionary<string, SpecBuildNote>>(ContextKeys.SpecBuildNotes, notes);
        if (relied is not null) await memory!.RecordReliedAsync(specId, relied.Citations, ct);
        return $"Build note for {specId} recorded ({lists.Sum(l => l.Entries.Count)} entries"
            + (relied is null ? string.Empty : $", relied on {relied.Citations.Count} memories")
            + "); a later read replaces only the fields it names." + Conflicts(note);
    }

    // 2026-10-13-8762n: what the note does against the series contract — shown, never refused.
    private string Conflicts(SpecBuildNote note)
    {
        if (!pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null) return string.Empty;
        var deviations = Specs.BuildNoteContractCheck.Deviations(note, specId, set);
        return deviations.Count == 0 ? string.Empty
            : " It conflicts with the series contract: " + string.Join("; ", deviations.Select(d =>
                $"\"{d.Entry}\" {d.Reason}{(d.Declared ? " (declared in your deviations)" : " (not declared in your deviations)")}")) + ".";
    }

    private static IReadOnlyList<string> Clean(string[]? entries) =>
        [.. (entries ?? []).Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim())];

    private static IEnumerable<string> Refusals((string Name, IReadOnlyList<string> Entries, bool Arrowed) list)
    {
        if (list.Entries.Count > MaxEntries)
            yield return $"'{list.Name}' has {list.Entries.Count} entries, at most {MaxEntries}";
        for (var i = 0; i < list.Entries.Count; i++)
        {
            var entry = list.Entries[i];
            if (entry.Length > MaxEntryChars)
                yield return $"'{list.Name}' entry {i + 1} is {entry.Length} characters, at most {MaxEntryChars}";
            else if (list.Arrowed && !IsArrowed(entry))
                yield return $"'{list.Name}' entry {i + 1} \"{entry}\" is not '<old> -> <new>'";
        }
    }

    private static bool IsArrowed(string entry)
    {
        var at = entry.IndexOf(Arrow, StringComparison.Ordinal);
        return at > 0 && at + Arrow.Length < entry.Length;
    }
}
