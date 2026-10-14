using AgentSmith.Contracts.Decisions;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets.Defaults;

/// <summary>
/// 2026-10-13-8762p: the whole batch is checked before anything is written. A design conversation's default is
/// answered there, and only an attempt's default can be overruled — the act is read off code runs only, so an
/// overrule of any other row would start nothing.
/// </summary>
public sealed class DefaultVerdictValidation
{
    public const int MaxItems = 100;
    public const int MaxNoteChars = 2000;
    public const string AnswerInConversation = "answer it in the design conversation";
    public const string OnlyAnAttempt = "only an attempt's default can be overruled";

    /// <summary>Null when every item may be written; else one line per refused item.</summary>
    public string? Problems(IReadOnlyList<DefaultVerdictRequest>? items, TicketDefaultsView listed)
    {
        if (items is not { Count: > 0 and <= MaxItems })
            return $"A verdict batch holds 1 to {MaxItems} items; this one holds {items?.Count ?? 0}. Nothing was written.";
        var byId = listed.Entries.ToDictionary(e => e.Id);
        var problems = items.Select((item, i) => Problem(item, byId) is { } why ? $"item {i + 1} (default #{item.Id}): {why}" : null)
            .OfType<string>().ToList();
        return problems.Count == 0 ? null : string.Join("\n", problems) + "\nNothing was written.";
    }

    private static string? Problem(DefaultVerdictRequest item, IReadOnlyDictionary<long, TicketDefaultView> listed)
    {
        if (item.Verdict is not (DefaultDecisionVerdicts.Ratified or DefaultDecisionVerdicts.Overruled))
            return $"the verdict '{item.Verdict}' is neither '{DefaultDecisionVerdicts.Ratified}' nor '{DefaultDecisionVerdicts.Overruled}'";
        if (item.Note is { Length: > MaxNoteChars })
            return $"the note has {item.Note.Length} characters, at most {MaxNoteChars} are kept";
        if (!listed.TryGetValue(item.Id, out var entry)) return "this ticket lists no such default";
        if (item.Verdict != DefaultDecisionVerdicts.Overruled) return null;
        if (entry.Conversation is not null) return $"a default from the design conversation cannot be overruled here — {AnswerInConversation}";
        return entry.Attempt is null ? OnlyAnAttempt : null;
    }
}
