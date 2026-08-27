using System.Text;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Builds the summarizer prompt for one compaction fold: the instruction that says
/// what a summary must preserve, plus the evicted middle rendered as bounded text.
/// Extracted from ChatClientFactory (2026-08-27-3eb1) — composing a prompt is not
/// the factory's job, and the aggregate bound below needs a home with tests.
///
/// <para>p0362: the summary must carry the CONCLUSION drawn from each file, not just
/// its name. "read WolverineExtension.cs" forces a re-read to recover what it said;
/// "WolverineExtension.cs defines the naming contract as X" does not. The re-read
/// spiral is the conclusion getting dropped — one level below the ticket-paraphrase
/// failure p0357 pinned away.</para>
///
/// <para>2026-08-27-3eb1: the serialized middle is bounded IN AGGREGATE. Per-result
/// truncation alone bounds nothing: a fold of 300 evicted messages at 2000 chars each
/// is a 600k-char summarizer prompt, so the call that exists to escape a context
/// overflow was itself able to cause one.</para>
/// </summary>
public sealed class CompactionSummaryRequest
{
    /// <summary>Aggregate ceiling on the rendered middle, ~50k tokens at 4 chars/token.</summary>
    public const int MaxSerializedChars = 200_000;

    private const int MaxResultChars = 2000;

    private const string SystemPrompt =
        "You are a context compactor for a coding agent's conversation. Summarize the "
        + "messages below, preserving: for each file read or modified, its path AND the "
        + "load-bearing conclusion the agent drew from it (the contract, API shape, "
        + "invariant, or fact it went looking for — 'X defines Y', never just 'read X'); "
        + "key decisions and their reasoning; error messages and how they were resolved; "
        + "and the current state of the implementation. Omit raw file contents, redundant "
        + "tool call/result pairs, and verbose command output (note only the outcome). "
        + "The agent must not need to re-read a file merely to recover a conclusion this "
        + "summary dropped. Be concise but complete — this summary continues the work.";

    /// <summary>The two-message prompt handed to the summarization client.</summary>
    public IList<ChatMessage> Build(IReadOnlyList<ChatMessage> middle) =>
    [
        new(ChatRole.System, SystemPrompt),
        new(ChatRole.User, Serialize(middle)),
    ];

    /// <summary>
    /// Renders the evicted middle as role-tagged lines, each tool result truncated and
    /// the whole rendering capped at <see cref="MaxSerializedChars"/>. The cap keeps the
    /// OLDEST messages: everything after the cut is still verbatim in the forwarded tail
    /// or the next fold, while the front of the middle is about to be dropped for good.
    /// </summary>
    public string Serialize(IReadOnlyList<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < messages.Count; i++)
        {
            if (sb.Length >= MaxSerializedChars)
                return sb.Append("…[").Append(messages.Count - i)
                    .Append(" of ").Append(messages.Count)
                    .AppendLine(" messages omitted — compaction serialization budget reached]")
                    .ToString();
            Append(sb, messages[i]);
        }
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, ChatMessage message)
    {
        var role = RoleLabel(message.Role);
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextContent t when !string.IsNullOrEmpty(t.Text):
                    sb.Append('[').Append(role).Append("] ").AppendLine(t.Text);
                    break;
                case FunctionCallContent call:
                    sb.Append("[Assistant] called ").AppendLine(call.Name);
                    break;
                case FunctionResultContent result:
                    var text = result.Result?.ToString() ?? string.Empty;
                    if (text.Length > MaxResultChars) text = text[..MaxResultChars] + " …[truncated]";
                    sb.Append("[Tool result] ").AppendLine(text);
                    break;
            }
        }
    }

    private static string RoleLabel(ChatRole role) =>
        role == ChatRole.Assistant ? "Assistant"
        : role == ChatRole.Tool ? "Tool"
        : role == ChatRole.System ? "System" : "User";
}
