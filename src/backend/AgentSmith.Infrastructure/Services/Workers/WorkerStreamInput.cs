using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// 2026-11-17-d6f5: the input part as Claude Code's stream-json input — one user message, one text block per input
/// block, on one line. The CLI marks the last block for caching and the API reads a cached prefix only where a block
/// ends, so the blocks the previous call ended with are read, not written again.
/// </summary>
public static class WorkerStreamInput
{
    public static string Line(IReadOnlyList<string> blocks) =>
        JsonSerializer.Serialize(new
        {
            type = "user",
            message = new { role = "user", content = blocks.Select(block => new { type = "text", text = block }) },
        }) + "\n";
}
