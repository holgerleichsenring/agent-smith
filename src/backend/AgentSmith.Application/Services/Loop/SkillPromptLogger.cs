using AgentSmith.Application.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Debug-level dump of the exact prompt + tool surface handed to the LLM for one skill
/// call. Off by default (Debug); enable via appsettings / Logging:LogLevel:
/// <c>"AgentSmith.Application.Services.Loop.SkillPromptLogger": "Debug"</c> when
/// diagnosing prompt composition. Each chat message is dumped in full with its role and
/// char count; tool names are listed.
/// <para>
/// p0423: extracted from SkillCallRuntime. Writing down what the model was asked is a
/// responsibility of its own, and it is the console-shaped ancestor of the run trace —
/// same question, an instrument nobody can read after the container is gone.
/// </para>
/// </summary>
public sealed class SkillPromptLogger(ILogger<SkillPromptLogger> logger)
{
    public void Log(SkillCallRequest request, IList<ChatMessage> messages, ChatOptions options)
    {
        if (!logger.IsEnabled(LogLevel.Debug)) return;
        for (var i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            var text = string.Join("\n", msg.Contents.OfType<TextContent>().Select(t => t.Text));
            logger.LogDebug(
                "skill_prompt skill={Skill} msg[{Index}/{Total}] role={Role} chars={Chars}\n{Text}",
                request.SkillName, i + 1, messages.Count, msg.Role, text.Length, text);
        }
        var toolNames = options.Tools?.OfType<AIFunction>().Select(t => t.Name).ToList() ?? [];
        logger.LogDebug(
            "skill_prompt skill={Skill} tools_offered={Count} names=[{Names}]",
            request.SkillName, toolNames.Count, string.Join(", ", toolNames));
    }
}
