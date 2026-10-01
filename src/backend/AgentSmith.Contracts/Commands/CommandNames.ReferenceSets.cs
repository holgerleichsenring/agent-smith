namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-10-01-283df: the step that hands a run the uploaded websites its approval cites. Its own
/// partial file, like the target probe's: CommandNames.Pipeline is over its length baseline.
/// </summary>
public static partial class CommandNames
{
    /// <summary>2026-10-01-283df: writes every website set the approved record cites into the
    /// carrying repository's sandbox at .agentsmith/reference/&lt;setId&gt;/, outside the commit,
    /// directly before PhaseSequence. A cited set this process cannot read fails the run naming it.</summary>
    public const string MaterializeReferenceSets = "MaterializeReferenceSetsCommand";
}
