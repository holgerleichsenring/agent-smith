namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0423: whether a run records its CONVERSATION as well as its numbers — every model
/// call's prompt and answer, and every tool result as the model received it.
/// <para>
/// Off by default: prompts grow from 145k to 4.1M characters within a single phase, so
/// this is a diagnostic instrument, not a default cost. On, it answers the question the
/// numbers cannot — WHY a run did what it did.
/// </para>
/// <para>
/// <see cref="Enabled"/> is overridden by the <c>AGENTSMITH_TRACE</c> environment
/// variable, because the two deployments that need it most — a k8s Job and a compose
/// service — set environment, not configuration files, and a diagnostic nobody can switch
/// on where the failure happens is no diagnostic.
/// </para>
/// </summary>
public sealed class TraceConfig
{
    public bool Enabled { get; init; }
}
