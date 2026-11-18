using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: the series master's thread across beats — each judged beat's prompt and the master's answer, in
/// order. A beat's tool calls stay in its run; what the master concluded is its answer, which the next beat reads. The
/// loop's compaction bounds what is sent, as for every master.
/// </summary>
public static class SeriesThread
{
    private sealed record Turn(string Role, string Text);

    public static IReadOnlyList<ChatMessage> Read(string json) =>
        [.. Turns(json).Select(t => new ChatMessage(t.Role == "assistant" ? ChatRole.Assistant : ChatRole.User, t.Text))];

    public static string Append(string json, string prompt, string answer) =>
        JsonSerializer.Serialize<IReadOnlyList<Turn>>([.. Turns(json), new Turn("user", prompt), new Turn("assistant", answer)]);

    private static IReadOnlyList<Turn> Turns(string json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<Turn>>(json) ?? [];
}
