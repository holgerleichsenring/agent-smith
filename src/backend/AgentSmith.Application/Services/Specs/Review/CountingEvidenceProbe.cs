namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: a probe that counts what it was asked and what it could not answer. The product policy
/// makes a NotChecked answer no problem at all, which is right for a finding and wrong for a count: the
/// plugin says never to treat a check that did not run as a pass, so the round says how many did not.
/// </summary>
internal sealed class CountingEvidenceProbe(IEvidenceProbe inner) : IEvidenceProbe
{
    public int Asked { get; private set; }

    public int NotChecked { get; private set; }

    public async Task<EvidenceProbeResult> ProbeAsync(string? qualifier, string path, CancellationToken ct)
    {
        var result = await inner.ProbeAsync(qualifier, path, ct);
        Asked++;
        if (result is EvidenceProbeResult.NotChecked) NotChecked++;
        return result;
    }
}
