using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-22-96c8a: the build note as one report's read names it, admitted entry by entry.</summary>
public sealed partial class SpecHandForward
{
    /// <summary>
    /// 2026-11-22-96c8a: the build note and memories read off a report, admitted entry by entry through the checks
    /// <see cref="HandForward"/> refuses a whole call on: the first 20 entries of a list, none over 300 characters,
    /// renamed and moved as '&lt;old&gt; -&gt; &lt;new&gt;', each relied_on resolved alone. What is dropped is logged, and so
    /// is the series-contract conflict text no model reads any more. A read overwrites only the note fields it names;
    /// the memories it names join the spec's earlier reliance, and an empty list never replaces one. Returns the
    /// recorded answer, or null when the read named no note field.
    /// </summary>
    public async Task<string?> RecordReadAsync(
        IReadOnlyList<string>? renamed, IReadOnlyList<string>? moved, IReadOnlyList<string>? persisted,
        IReadOnlyList<string>? deviations, IReadOnlyList<string>? reliedOn, CancellationToken ct = default)
    {
        var dropped = new List<string>();
        var lists = new[]
        {
            Admit("renamed", renamed, true, dropped), Admit("moved", moved, true, dropped),
            Admit("persisted", persisted, false, dropped), Admit("deviations", deviations, false, dropped),
        };
        var relied = await AdmitReliedAsync(reliedOn, dropped, ct);
        if (dropped.Count > 0)
            logger?.LogWarning("Build note for {Spec}: dropped from the report's read: {Dropped}", specId, string.Join("; ", dropped));
        await RecordReliedUnionAsync(relied, ct);
        if (lists.All(l => l.Length == 0)) return null;
        var held = Note();
        var answer = await HandForward(
            Pick(lists[0], held?.Renamed), Pick(lists[1], held?.Moved), Pick(lists[2], held?.Persisted), Pick(lists[3], held?.Deviations),
            relied_on: null, ct);
        logger?.LogInformation("Build note for {Spec} from the report: {Answer}", specId, answer);
        return answer;
    }

    private static string[] Pick(string[] read, IReadOnlyList<string>? held) => read.Length > 0 ? read : [.. held ?? []];

    private Contracts.Specs.SpecBuildNote? Note() =>
        pipeline.TryGet<IReadOnlyDictionary<string, Contracts.Specs.SpecBuildNote>>(Contracts.Commands.ContextKeys.SpecBuildNotes, out var notes)
        && notes is not null && notes.TryGetValue(specId, out var note) ? note : null;

    // The memories this read names, added to what the spec relied on before; nothing named, nothing recorded.
    private async Task RecordReliedUnionAsync(string[] relied, CancellationToken ct)
    {
        if (memory is null || relied.Length == 0) return;
        var read = await memory.ResolveAsync(relied, ct);
        var earlier = pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<Contracts.Memory.MemoryCitation>>>(
            Contracts.Commands.ContextKeys.MemoryReliance, out var held) && held is not null && held.TryGetValue(specId, out var cited)
            ? cited : [];
        var union = earlier.Concat(read.Citations).Distinct().ToList();
        if (union.Count > earlier.Count) await memory.RecordReliedAsync(specId, union, ct);
    }

    private static string[] Admit(string name, IReadOnlyList<string>? entries, bool arrowed, List<string> dropped)
    {
        var clean = Clean(entries is null ? null : [.. entries]);
        var kept = new List<string>();
        foreach (var entry in clean)
        {
            if (kept.Count >= MaxEntries) { dropped.Add($"'{name}' entry beyond the first {MaxEntries}: \"{entry}\""); continue; }
            if (entry.Length > MaxEntryChars) { dropped.Add($"'{name}' entry of {entry.Length} characters, at most {MaxEntryChars}"); continue; }
            if (arrowed && !IsArrowed(entry)) { dropped.Add($"'{name}' entry \"{entry}\" is not '<old> -> <new>'"); continue; }
            kept.Add(entry);
        }
        return [.. kept];
    }

    private async Task<string[]> AdmitReliedAsync(IReadOnlyList<string>? entries, List<string> dropped, CancellationToken ct)
    {
        var clean = Clean(entries is null ? null : [.. entries]);
        if (memory is null || clean.Count == 0) return [];
        var kept = new List<string>();
        foreach (var entry in clean.Take(MaxEntries))
        {
            var read = await memory.ResolveAsync([entry], ct);
            if (read.Problems.Count == 0) kept.Add(entry);
            else dropped.Add($"relied_on {string.Join(", ", read.Problems)}");
        }
        return [.. kept];
    }
}
