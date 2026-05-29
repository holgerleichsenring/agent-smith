namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: one sub-agent's task description as the master emits it through
/// the <c>spawn_agents</c> tool. <see cref="Name"/> + <see cref="Activity"/>
/// are required and code-validated non-generic (see
/// <c>SubAgentNameValidator</c>) so the dashboard's identity story does not
/// rot. <see cref="InheritedContext"/> carries the data slot the child needs
/// to do its work without re-discovering the run goal.
/// </summary>
public sealed record SubAgentSpec(
    string Name,
    string Activity,
    string TaskDescription,
    InheritedContext InheritedContext,
    string? OutputHint = null,
    ToolProfile ToolProfile = ToolProfile.Investigator);
