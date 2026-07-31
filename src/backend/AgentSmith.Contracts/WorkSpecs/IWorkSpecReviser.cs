namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: applies one master-issued revision — guards, then commit + push on the
/// ticket branch. Returns the message the tool hands back to the model, so a
/// refusal reads as an answer rather than an exception.
/// </summary>
public interface IWorkSpecReviser
{
    Task<string> ReviseAsync(WorkSpecRevisionRequest request, CancellationToken cancellationToken);
}
