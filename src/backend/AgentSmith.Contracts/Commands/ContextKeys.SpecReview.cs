namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-09-17-042eh: the keys a spec's start and its sequence carry. Their own file,
/// because <see cref="ContextKeys"/>' spec partial is at the length limit and a key the
/// ratchet forced into a baseline row would be the first of many.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>2026-09-17-042eh: sandbox key → the commit the sandbox stood at when the
    /// CURRENT spec was selected (IReadOnlyDictionary&lt;string, string&gt;). The spec
    /// review diffs from here to the working tree, so it reads this spec's own work and
    /// not the run's. Rewritten at every SelectSpec.</summary>
    public const string SpecStartHeads = "SpecStartHeads";

    /// <summary>2026-10-25-54beb: the run's owner of its opened template scopes, installed and
    /// disposed by the pipeline executor; the master and the diff reviewer's look borrow from it.
    /// A live handle, never checkpointed.</summary>
    public const string RunTemplateScopes = "RunTemplateScopes";

    /// <summary>2026-11-18-c8e2d: the run's sub-agent team — every child its masters started, so a later pass of a spec
    /// continues the child that built it. A live handle, never checkpointed.</summary>
    public const string SubAgentTeam = "SubAgentTeam";
}
