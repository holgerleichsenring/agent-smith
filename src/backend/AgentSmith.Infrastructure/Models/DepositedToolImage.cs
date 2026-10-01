using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// 2026-10-01-283dd: a <see cref="ToolImage"/> together with the tool call that deposited it,
/// so the loop can name the tool and the call next to the picture.
/// </summary>
public sealed record DepositedToolImage(ToolImage Image, string ToolName, string CallId);
