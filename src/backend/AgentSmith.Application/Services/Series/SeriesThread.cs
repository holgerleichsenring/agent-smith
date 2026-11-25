using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: the series master's thread across beats — each judged beat's prompt and the master's answer, in
/// order. A beat's tool calls stay in its run; what the master concluded is its answer, which the next beat reads. The
/// loop's compaction bounds what is sent, as for every master.
/// <para>2026-11-25-433fb: a thread grown past its bound is compacted (<see cref="CompactSeriesThreadHandler"/>): its older
/// pairs become one pair holding the master's notes, and the pairs after them stay as stored.</para>
/// </summary>
public static class SeriesThread
{
    /// <summary>2026-11-25-433fb: the user side of the pair that holds the master's notes.</summary>
    public const string NotesHead = "[Your notes from earlier beats, written when your thread was compacted]";

    private sealed record Turn(string Role, string Text);

    public static IReadOnlyList<ChatMessage> Read(string json) => Messages(Turns(json));

    public static string Append(string json, string prompt, string answer) =>
        JsonSerializer.Serialize<IReadOnlyList<Turn>>([.. Turns(json), new Turn("user", prompt), new Turn("assistant", answer)]);

    /// <summary>2026-11-25-433fb: the characters the thread carries.</summary>
    public static int Size(string json) => Turns(json).Sum(t => t.Text.Length);

    /// <summary>2026-11-25-433fb: the characters the first <paramref name="pairs"/> pairs carry.</summary>
    public static int Size(string json, int pairs) => Turns(json).Take(pairs * 2).Sum(t => t.Text.Length);

    /// <summary>2026-11-25-433fb: the beats (prompt and answer pairs) the thread holds, an earlier notes pair among them.</summary>
    public static int Pairs(string json) => Turns(json).Count / 2;

    /// <summary>2026-11-25-433fb: the first <paramref name="pairs"/> pairs, as the notes call reads them.</summary>
    public static IReadOnlyList<ChatMessage> Older(string json, int pairs) => Messages([.. Turns(json).Take(pairs * 2)]);

    /// <summary>2026-11-25-433fb: the thread with its first <paramref name="pairs"/> pairs replaced by the notes pair.</summary>
    public static string Compact(string json, int pairs, string notes) =>
        JsonSerializer.Serialize<IReadOnlyList<Turn>>(
            [new Turn("user", NotesHead), new Turn("assistant", notes), .. Turns(json).Skip(pairs * 2)]);

    private static IReadOnlyList<ChatMessage> Messages(IReadOnlyList<Turn> turns) =>
        [.. turns.Select(t => new ChatMessage(t.Role == "assistant" ? ChatRole.Assistant : ChatRole.User, t.Text))];

    private static IReadOnlyList<Turn> Turns(string json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<Turn>>(json) ?? [];
}
