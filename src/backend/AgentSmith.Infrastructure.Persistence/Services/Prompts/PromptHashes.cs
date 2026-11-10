using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Prompts;

namespace AgentSmith.Infrastructure.Persistence.Services.Prompts;

/// <summary>
/// 2026-11-09-0620a: how a block and a record are named. A block by its kind, role and content, so the same message
/// sent on the next iteration finds its row; a record by its run and its block sequence, so a retried call of the same
/// prompt is the same record and two runs never share one.
/// </summary>
public static class PromptHashes
{
    public static string Of(PromptBlock block) =>
        Hex($"{block.Kind}\n{block.Role}\n{block.Content}");

    public static string Record(string runId, IReadOnlyList<string> blockHashes) =>
        Hex(runId + "\n" + string.Join("\n", blockHashes));

    private static string Hex(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
