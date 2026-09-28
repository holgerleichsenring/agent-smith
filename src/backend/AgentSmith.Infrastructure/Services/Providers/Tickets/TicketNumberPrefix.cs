using System.Globalization;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-28-1da5a: the ticket numbers a partly-typed one stands for.
/// <para>
/// An operator typing "194" means the work items whose number BEGINS with it — which is what a
/// tracker's own search box answers and what an exact lookup cannot. Three of the four trackers
/// can name a set of ids: Azure DevOps as numeric ranges, Jira as an enumerated key list, GitLab
/// as an enumerated iid list. One mechanism, three spellings.
/// </para>
/// <para>
/// TWO EXTRA DIGITS, AND THE SAME BOUND EVERYWHERE. Ranges scale and enumeration does not: a third
/// extra digit is one more disjunct on Azure DevOps and a thousand more enumerated values on the
/// other two. Holding all three to the same depth is what makes the feature behave alike rather
/// than merely exist alike — so "194" reaches 19499 and not 194999, and a board whose ids run
/// three digits past what was typed is a board this does not help with. Said here rather than
/// discovered.
/// </para>
/// </summary>
internal static class TicketNumberPrefix
{
    /// <summary>How many digits may follow what was typed.</summary>
    internal const int ExtraDigits = 2;

    /// <summary>The typed text as a prefix, or null when it is not a plain number.</summary>
    public static string? Of(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length > 0 && trimmed.All(char.IsAsciiDigit) ? trimmed : null;
    }

    /// <summary>
    /// The inclusive ranges the prefix covers — the number itself, then each wider decade.
    /// </summary>
    public static IReadOnlyList<(long From, long To)> Ranges(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (!long.TryParse(prefix, NumberStyles.None, CultureInfo.InvariantCulture, out var typed))
            return [];
        var ranges = new List<(long, long)> { (typed, typed) };
        var from = typed;
        var width = 1L;
        for (var digit = 0; digit < ExtraDigits; digit++)
        {
            from *= 10;
            width *= 10;
            ranges.Add((from, from + width - 1));
        }

        return ranges;
    }

    /// <summary>The same set enumerated, for a tracker that can only name ids one by one.</summary>
    public static IReadOnlyList<long> Ids(string prefix) =>
        [.. Ranges(prefix).SelectMany(range => LongRange(range.From, range.To))];

    private static IEnumerable<long> LongRange(long from, long to)
    {
        for (var id = from; id <= to; id++) yield return id;
    }
}
