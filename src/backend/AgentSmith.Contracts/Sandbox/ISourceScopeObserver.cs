namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// Told when a source scope begins opening its repository and how that ended. Each state is
/// reported once per opening, from inside the scope's materialisation gate.
/// <para>
/// An implementation must not throw: the scope awaits the report on the path that serves a
/// read, and a progress line that failed the read would cost the answer it announces.
/// </para>
/// </summary>
public interface ISourceScopeObserver
{
    Task ReportAsync(string repoName, SourceScopeProgress progress, CancellationToken cancellationToken);
}
