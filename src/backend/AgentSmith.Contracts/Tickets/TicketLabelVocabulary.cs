using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-09-25-3c7ac: the names ONE tracker's board uses for the labels this framework writes —
/// the seven lifecycle words and the approved-set stamp. Eight names, because those are the eight
/// the code actually writes; the phase word a person types, the record label and the position
/// prefix have no writer and can only be RECOGNISED, so they stay constants.
/// <para>
/// WRITING IS CONFIGURED, READING IS A UNION THAT NEVER SHRINKS. What goes onto a ticket is the
/// configured name; what is recognised coming back is the configured name AND every name this
/// framework has ever written. Tickets carrying the old words sit on boards nobody will migrate,
/// and a reader that forgets them misreads history on the day it ships.
/// </para>
/// <para>
/// An operator who configures nothing gets <see cref="Default"/>, which is today's vocabulary
/// exactly — silence must not change a board.
/// </para>
/// </summary>
public sealed class TicketLabelVocabulary
{
    /// <summary>The configuration key naming the approved-set stamp; the other eight are the
    /// lifecycle state names <see cref="LifecycleLabels.TryParseName"/> already parses.</summary>
    public const string ApprovedSetKey = "approved-set";

    private readonly Dictionary<TicketLifecycleStatus, string> _lifecycle;

    public TicketLabelVocabulary(IReadOnlyDictionary<string, string>? configured = null)
    {
        _lifecycle = [];
        ApprovedSetStamp = TicketLabels.ApprovedSetStamp;
        foreach (var (key, name) in configured ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (LifecycleLabels.TryParseName(key, out var status)) _lifecycle[status] = name.Trim();
            else if (IsApprovedSetKey(key)) ApprovedSetStamp = name.Trim();
        }
    }

    /// <summary>Today's names, and what a tracker that configures nothing gets.</summary>
    public static TicketLabelVocabulary Default { get; } = new();

    /// <summary>The vocabulary of one tracker, spelled in one place.</summary>
    public static TicketLabelVocabulary For(Models.Configuration.TrackerConnection tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        return tracker.LabelNames.Count == 0 ? Default : new TicketLabelVocabulary(tracker.LabelNames);
    }

    /// <summary>What this board calls the stamp a filing writes.</summary>
    public string ApprovedSetStamp { get; }

    /// <summary>What this board calls a lifecycle state.</summary>
    public string For(TicketLifecycleStatus status) =>
        _lifecycle.TryGetValue(status, out var name) ? name : LifecycleLabels.For(status);

    /// <summary>True for a label this framework owns as a lifecycle word — under this board's
    /// name or under any this framework has ever written.</summary>
    public bool IsLifecycleLabel(string label) => TryParse(label, out _);

    /// <summary>The state a label names, under this board's vocabulary or the historical one.</summary>
    public bool TryParse(string label, out TicketLifecycleStatus status)
    {
        foreach (var (state, name) in _lifecycle)
        {
            if (!string.Equals(label, name, StringComparison.OrdinalIgnoreCase)) continue;
            status = state;
            return true;
        }
        return LifecycleLabels.TryParse(label, out status);
    }

    /// <summary>True when the stamp on this ticket is ours — configured or historical.</summary>
    public bool IsApprovedSetStamp(string label) =>
        string.Equals(label, ApprovedSetStamp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(label, TicketLabels.ApprovedSetStamp, StringComparison.OrdinalIgnoreCase);

    private static bool IsApprovedSetKey(string key) =>
        string.Equals(
            key.Trim().Replace('_', '-'), ApprovedSetKey, StringComparison.OrdinalIgnoreCase);
}
