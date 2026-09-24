using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-23-4722a: turns the tools a call already carries into the definitions a session is told
/// about.
///
/// The schema is the one AIFunctionFactory already produced, passed through verbatim — there is no
/// second definition to keep in step. What is deliberately NOT passed through is the body: a
/// declaration the runtime can invoke is one it WILL invoke, and the tool loop would move inside
/// the session, where none of our decorators can re-enter it.
/// </summary>
internal static class CopilotToolProjection
{
    internal static IReadOnlyList<CopilotToolDefinition> From(IEnumerable<AITool> tools) =>
        tools.OfType<AIFunctionDeclaration>()
            .Select(t => new CopilotToolDefinition(t.Name, t.Description, t.JsonSchema))
            .ToList();
}
