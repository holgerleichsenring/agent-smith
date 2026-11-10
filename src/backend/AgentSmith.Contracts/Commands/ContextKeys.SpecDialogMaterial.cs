namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-10-14a3d: what a design turn's conversation holds that decides which tools it is offered.</summary>
public static partial class ContextKeys
{
    /// <summary><c>bool</c> — the conversation holds a mock already, so its mock tools stay offered.</summary>
    public const string SpecDialogHasMocks = "SpecDialogHasMocks";
}
