namespace AgentSmith.Application.Services.Output;

/// <summary>
/// Picks the directory a pipeline's delivered files land in: the one the operator
/// named, else <c>/output</c> (the container mount), else <c>./agentsmith-output</c>,
/// else the temp directory — the first that can actually be written.
/// </summary>
public interface IOutputDirectoryResolver
{
    string Resolve(string? requested);
}
