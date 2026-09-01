using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: reads the refutations out of whatever the model wrote around them. Null is a
/// FAILED call — not "everything is substantiated" and not "everything is refuted" — and
/// the caller must be able to tell that apart from a real verdict.
/// </summary>
public interface IFindingRefutationReader
{
    IReadOnlyList<FindingRefutation>? Read(string? text);
}
