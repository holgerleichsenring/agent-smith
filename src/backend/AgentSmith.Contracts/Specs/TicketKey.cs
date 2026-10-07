namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-06-03c7c: the identity of one ticket across trackers — <c>&lt;provider&gt;-&lt;ticketId&gt;</c>,
/// lowercased with every non-alphanumeric character collapsed. It keys the approval record, the
/// series pointer and the design conversation. 2026-10-06-03c7d: where a ticket's files lie is
/// <see cref="SeriesPaths"/>' question — the manifest naming this key is found on the branch.
/// </summary>
public readonly record struct TicketKey(string Value)
{
    private const string Unknown = "unknown";

    public static TicketKey For(string provider, string ticketId) =>
        new($"{Slug(provider)}-{Slug(ticketId)}");

    public override string ToString() => Value;

    private static string Slug(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Unknown;
        var chars = raw.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        return new string(chars).Trim('-') is { Length: > 0 } s ? s : Unknown;
    }
}
