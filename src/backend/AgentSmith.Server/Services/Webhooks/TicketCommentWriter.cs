using System.Text.Json;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>2026-11-25-9992g: the author of an issue comment on a tracker that is a repository, in the terms the host's
/// write-access check reads (the same a pull-request comment's author carries); null when the payload does not name one.</summary>
public static class TicketCommentWriter
{
    /// <summary>A GitHub issue_comment delivery: the repository's full name and the commenter's login.</summary>
    public static PrCommentAuthor? GitHub(JsonElement root)
    {
        var login = PayloadActTime.Text(root, "comment", "user", "login");
        var repository = PayloadActTime.Text(root, "repository", "full_name");
        return login is null || repository is null ? null
            : new PrCommentAuthor(PayloadActTime.Text(root, "repository", "html_url") ?? string.Empty, repository, login, login);
    }

    /// <summary>A GitLab Note Hook delivery: the project's id and the commenter's user id.</summary>
    public static PrCommentAuthor? GitLab(JsonElement root)
    {
        var user = Number(root, "user", "id");
        var project = Number(root, "project", "id");
        return user is null || project is null ? null
            : new PrCommentAuthor(PayloadActTime.Text(root, "project", "web_url") ?? string.Empty, project, user,
                PayloadActTime.Text(root, "user", "username") ?? user);
    }

    private static string? Number(JsonElement root, string parent, string name) =>
        root.TryGetProperty(parent, out var element) && element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64().ToString() : null;
}
