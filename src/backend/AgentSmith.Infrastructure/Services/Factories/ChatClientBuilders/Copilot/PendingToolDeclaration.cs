using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-23-4722a: a tool the Copilot runtime knows about and will NOT run.
///
/// The SDK decides by asking the declaration for an AIFunction: "Tools backed by an AIFunction are
/// invoked automatically. Declaration-only tools are left PENDING for the client to resolve via the
/// external tool request event." <see cref="AIFunction"/> derives from
/// <see cref="AIFunctionDeclaration"/>, so handing the session our real tools would hand it the tool
/// loop as well — and every decorator that re-enters per iteration (rate limiting, cost events, the
/// run trace, the iteration cap) would see one turn instead of one call.
///
/// This subclass carries the name, description and schema AIFunctionFactory already produced, and
/// nothing else. <c>GetService&lt;AIFunction&gt;()</c> on it returns null, which is exactly the
/// question the runtime asks.
/// </summary>
internal sealed class PendingToolDeclaration(string name, string description, JsonElement schema)
    : AIFunctionDeclaration
{
    public override string Name { get; } = name;

    public override string Description { get; } = description;

    public override JsonElement JsonSchema { get; } = schema;
}
