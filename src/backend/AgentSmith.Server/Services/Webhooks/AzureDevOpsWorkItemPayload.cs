using System.Text.Json;
using AgentSmith.Application.Services.Specs;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9a: the work item a work-item service hook is about. Microsoft documents two
/// shapes for workitem.updated — a WorkItemUpdate whose resource.id is the UPDATE number, with
/// the whole item under resource.revision, and the flat work item today's sample shows — while
/// workitem.commented is always the flat item. Whether resource.revision is present decides;
/// the update number is never read as the ticket id.
/// </summary>
public static class AzureDevOpsWorkItemPayload
{
    public static (int WorkItemId, JsonElement Fields) Read(JsonElement resource)
    {
        var item = resource.TryGetProperty("revision", out var revision)
            && revision.ValueKind == JsonValueKind.Object
                ? revision
                : resource;
        return (item.GetProperty("id").GetInt32(), item.GetProperty("fields"));
    }

    /// <summary>The comment of a workitem.commented hook: System.History, as text.</summary>
    public static string CommentText(JsonElement fields) =>
        fields.TryGetProperty("System.History", out var history) && history.ValueKind == JsonValueKind.String
            ? TicketHtmlConverter.ToText(history.GetString())
            : string.Empty;

    public static string State(JsonElement fields) =>
        fields.TryGetProperty("System.State", out var state) && state.ValueKind == JsonValueKind.String
            ? state.GetString() ?? string.Empty
            : string.Empty;
}
