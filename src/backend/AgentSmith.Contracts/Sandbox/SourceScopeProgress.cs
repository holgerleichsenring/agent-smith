namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// How far a source scope has got with opening its repository. Failed is a state of its own:
/// a scope whose preparation threw is not open, so without it the line an observer drew for
/// "opening" would never end.
/// </summary>
public enum SourceScopeProgress
{
    Opening,
    Ready,
    Failed,
}
