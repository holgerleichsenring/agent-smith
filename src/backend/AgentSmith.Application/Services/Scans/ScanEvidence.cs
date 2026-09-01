namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429a: everything a scan can check one of its own findings against.
/// <para>
/// p0429 had a single source of evidence — the files the scan could read — and an api-scan
/// often has no source at all (its checkout is fail-soft) while its findings point at a
/// live system. One bundle now, so a resolver takes the evidence that answers ITS kind of
/// citation and the substantiator does not care which kind arrived.
/// </para>
/// </summary>
public sealed record ScanEvidence(
    IScanSourceReader? Source,
    CitedEndpointIndex Endpoints,
    ScanExchanges Exchanges)
{
    /// <summary>Nothing to check against — every finding passes through untouched.</summary>
    public static ScanEvidence None { get; } =
        new(null, CitedEndpointIndex.Empty, ScanExchanges.Empty);

    public bool IsEmpty => Source is null && Endpoints.IsEmpty;
}
