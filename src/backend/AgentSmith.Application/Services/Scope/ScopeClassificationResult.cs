namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413a: what the one scope call came back with. The repo verdict and the
/// ticket estimate are separate fields because they are separate answers with
/// separate failure modes — <paramref name="Error"/> describes the SCOPE
/// verdict only, and an errored scope still carries whatever estimate the reply
/// stated.
/// </summary>
/// <param name="Classification">Null when the reply was not a usable repo verdict;
/// the scope evaluator then keeps all repositories.</param>
/// <param name="Estimate">The ticket's size and shape — <see cref="ScopeEstimate.None"/>
/// when the reply stated neither.</param>
/// <param name="Error">The scope-verdict failure, when any, for the run record.</param>
public sealed record ScopeClassificationResult(
    RepoScopeClassification? Classification, ScopeEstimate Estimate, string? Error);
