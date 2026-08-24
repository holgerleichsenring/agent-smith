namespace AgentSmith.Server.Contracts;

/// <summary>
/// p0503e: whether the configured token authority can be reached, and the one pass that
/// finds out.
/// <para>
/// A request cannot work this out for itself. Measured against a stub authority that could
/// be taken down at will: the exception reaching the pipeline is an invalid-issuer
/// exception with NO inner exception, because the configuration manager absorbs the fetch
/// failure — so "the authority is down" and "this token has the wrong issuer" are the same
/// event by the time anything can look at them. The refusal path asks this instead.
/// </para>
/// </summary>
internal interface IAuthorityReachability
{
    /// <summary>True from the moment a pass fails until a pass succeeds.</summary>
    bool IsUnreachable { get; }

    /// <summary>One pass: fetch the discovery document and publish what happened.</summary>
    Task ProbeAsync(CancellationToken cancellationToken);
}
