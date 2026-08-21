using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// p0490: reads GitLab's answer to a merge request merge. A success body carries the
/// merge request's resulting <c>state</c>; a refusal carries a <c>message</c> — the
/// sentence naming the approval, pipeline or protected branch that stopped it. Both
/// live in the same JSON envelope, so one parser reads both.
/// </summary>
public sealed record GitLabMergeAnswer(string? State, string? Message)
{
    public static GitLabMergeAnswer Parse(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            return new GitLabMergeAnswer(
                root.TryGetProperty("state", out var state) ? state.GetString() : null,
                root.TryGetProperty("message", out var message) ? message.ToString() : null);
        }
        catch (JsonException)
        {
            return new GitLabMergeAnswer(null, body);
        }
    }
}
