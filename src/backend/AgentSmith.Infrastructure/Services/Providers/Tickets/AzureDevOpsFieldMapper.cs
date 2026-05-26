using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: maps Azure DevOps work-item field dictionaries onto the canonical
/// <see cref="Ticket"/>. ADO returns work-item fields as
/// <see cref="IDictionary{TKey,TValue}"/> keyed by reference name
/// (<c>System.Title</c>, <c>System.Description</c>, etc.).
/// Tags are a single semicolon-separated string and split here.
/// </summary>
public sealed class AzureDevOpsFieldMapper : ITicketFieldMapper<IDictionary<string, object>>
{
    public Ticket Map(TicketId ticketId, IDictionary<string, object> fields)
    {
        var tagsRaw = Read(fields, "System.Tags");
        string[] labels = string.IsNullOrEmpty(tagsRaw)
            ? []
            : tagsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new Ticket(
            ticketId,
            Read(fields, "System.Title"),
            Read(fields, "System.Description"),
            ReadOrNull(fields, "Microsoft.VSTS.Common.AcceptanceCriteria"),
            Read(fields, "System.State"),
            "AzureDevOps",
            labels);
    }

    private static string Read(IDictionary<string, object> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value?.ToString() ?? "" : "";

    private static string? ReadOrNull(IDictionary<string, object> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value?.ToString() : null;
}
