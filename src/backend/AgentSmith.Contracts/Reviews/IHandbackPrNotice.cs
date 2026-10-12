using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// Posts a hand-back on the ticket's pull requests as well as the ticket, so a reviewer who works on
/// the pull request reads it there and can answer with a comment command. Fail-soft per pull request.
/// </summary>
public interface IHandbackPrNotice
{
    Task PostAsync(PipelineContext pipeline, string handback, CancellationToken cancellationToken);
}
