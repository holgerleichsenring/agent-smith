using System.Text.Json;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// 2026-11-12-cd4ca: scripts the design master's proposal writes — each a tool call into the turn's draft, the way
/// the master proposes now that its reply is prose.
/// </summary>
public static class DraftWriteScript
{
    public static ScriptedChatClient EnqueueSpecWrite(this ScriptedChatClient client, string yaml, bool parent = false) =>
        client.EnqueueToolCall("write_spec", JsonSerializer.Serialize(new { yaml, parent }));

    public static ScriptedChatClient EnqueueBugWrite(this ScriptedChatClient client, string title, string description) =>
        client.EnqueueToolCall("write_bug", JsonSerializer.Serialize(new { title, description }));

    public static ScriptedChatClient EnqueueTicketWrite(
        this ScriptedChatClient client, string key, string title, string requirement, string[] done,
        string[]? requires = null, string[]? repos = null) =>
        client.EnqueueToolCall("write_ticket",
            JsonSerializer.Serialize(new { key, title, requirement, done, requires = requires ?? [], repos = repos ?? [] }));

    /// <summary>An epic: its parent, then each child in order.</summary>
    public static ScriptedChatClient EnqueueEpicWrite(this ScriptedChatClient client, string parent, params string[] children)
    {
        client.EnqueueSpecWrite(parent, parent: true);
        foreach (var child in children) client.EnqueueSpecWrite(child);
        return client;
    }
}
