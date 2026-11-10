namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0423: whether a run records its CONVERSATION as well as its numbers — every model
/// call's answer, and every tool result as the model received it. 2026-11-09-0620d: the
/// prompts are not behind this switch: every call's prompt is stored always, deduplicated
/// (2026-11-09-0620a), and opened from the run's call table.
/// <para>
/// Off by default: prompts grow from 145k to 4.1M characters within a single spec, so
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
