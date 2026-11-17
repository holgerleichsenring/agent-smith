namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: what a filing has done so far — the report's tickets, the created tickets the start pass
/// settles, each plan key's tracker id and title, and the notes. A filing that stops reports exactly this.
/// </summary>
public sealed class FilingProgress
{
    public List<FiledTicket> Filed { get; } = [];

    public List<CreatedGraphTicket> Tickets { get; } = [];

    public List<string> Notes { get; } = [];

    public Dictionary<string, (string Id, string Title)> Ids { get; } = new(StringComparer.Ordinal);

    /// <summary>Records a ticket the tracker just created under its plan key.</summary>
    public void Created(string key, FiledTicket filed, CreatedGraphTicket created)
    {
        ArgumentNullException.ThrowIfNull(filed);
        ArgumentNullException.ThrowIfNull(created);
        Filed.Add(filed);
        Tickets.Add(created);
        Ids[key] = (created.Ticket.Id.Value, filed.Title);
    }

    /// <summary>Changes the last created ticket — its edges once stored, its completion once recorded.</summary>
    public void Update(Func<CreatedGraphTicket, CreatedGraphTicket> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Tickets[^1] = change(Tickets[^1]);
    }
}
