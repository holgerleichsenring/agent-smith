using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// 2026-11-12-cd4ca: scripts the design master's proposal writes — each a tool call into the turn's draft, the way
/// the master proposes now that its reply is prose. 2026-11-15-06fc: each a write_file of the item's draft file; a
/// spec's file is named by the id its text states.
/// </summary>
public static partial class DraftWriteScript
{
    public static ScriptedChatClient EnqueueSpecWrite(this ScriptedChatClient client, string yaml, bool parent = false) =>
        client.EnqueueFileWrite($"draft:{(parent ? "epic" : "specs")}/{StatedId(yaml)}.yaml", yaml);

    public static ScriptedChatClient EnqueueBugWrite(this ScriptedChatClient client, string title, string description) =>
        client.EnqueueFileWrite("draft:bug.yaml", $"title: {JsonSerializer.Serialize(title)}\ndescription: {JsonSerializer.Serialize(description)}\n");

    public static ScriptedChatClient EnqueueTicketWrite(
        this ScriptedChatClient client, string key, string title, string requirement, string[] done,
        string[]? requires = null, string[]? repos = null) =>
        client.EnqueueFileWrite($"draft:tickets/{key}.yaml", JsonSerializer.Serialize(
            new { title, requirement, done, requires = requires ?? [], repos = repos ?? [] }));

    /// <summary>An epic: its parent, then each child in order.</summary>
    public static ScriptedChatClient EnqueueEpicWrite(this ScriptedChatClient client, string parent, params string[] children)
    {
        client.EnqueueSpecWrite(parent, parent: true);
        foreach (var child in children) client.EnqueueSpecWrite(child);
        return client;
    }

    /// <summary>One write_file of a draft file.</summary>
    public static ScriptedChatClient EnqueueFileWrite(this ScriptedChatClient client, string path, string content) =>
        client.EnqueueToolCall("write_file", JsonSerializer.Serialize(new { path, content }));

    private static string StatedId(string yaml) => SpecLine().Match(yaml) is { Success: true } match ? match.Groups[1].Value : "p1";

    [GeneratedRegex(@"^\s*spec:\s*(\S+)", RegexOptions.Multiline)]
    private static partial Regex SpecLine();
}
