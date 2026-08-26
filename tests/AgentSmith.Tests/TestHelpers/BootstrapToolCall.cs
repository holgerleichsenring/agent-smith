using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-08-26-167c: calls <c>write_context_yaml</c> out of a bootstrap round's tool
/// surface, the way the producer skill does.
/// <para>
/// A round is green only when THIS round wrote a context.yaml, so a chat stub that
/// returns prose and calls nothing is no longer a passing round — which is the point
/// of the change, and means every stub that asserts success has to do the write.
/// </para>
/// </summary>
internal static class BootstrapToolCall
{
    /// <summary>A document that satisfies the gate: a workdir and a stack image.</summary>
    public const string ValidDocument =
        """{ "meta": { "workdir": "server" }, "stack": { "lang": "C#", "image": "mcr.microsoft.com/dotnet/sdk:8.0" } }""";

    /// <summary>A document the gate refuses: a stack that names no image.</summary>
    public const string RefusedDocument =
        """{ "meta": { "workdir": "server" }, "stack": { "lang": "C#" } }""";

    public static async Task<string?> WriteContextYamlAsync(
        ChatOptions? options, string document, string contextName = "server")
    {
        var tool = options?.Tools?.OfType<AIFunction>()
            .FirstOrDefault(function => function.Name == WriteContextYamlToolHost.ToolName);
        if (tool is null) return null;
        var result = await tool.InvokeAsync(
            new AIFunctionArguments
            {
                ["repo"] = string.Empty,
                ["context_name"] = contextName,
                ["document"] = JsonDocument.Parse(document).RootElement,
            },
            CancellationToken.None);
        return result?.ToString();
    }
}
