namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: whether one fact's evidence stands — every cited path a file, every cited
/// line inside it, every observation dated — under the caller's <see cref="EvidencePolicy"/> and
/// against the caller's <see cref="IEvidenceProbe"/>. The probe is a parameter because it is the
/// part that differs per caller: a working tree here, a turn's sandboxes in the product.
/// </summary>
public sealed class EvidenceCheck(EvidenceReferences references)
{
    public async Task<IReadOnlyList<EvidenceProblem>> CheckAsync(
        string evidence, EvidencePolicy policy, IEvidenceProbe probe, CancellationToken ct)
    {
        var reading = references.Read(evidence);
        var problems = new List<EvidenceProblem>();
        if (reading.IsEmpty && policy.NoReferenceIsProblem)
            problems.Add(new EvidenceProblem("cites no path, look line or dated observation"));
        if (!policy.AllowMinted)
            problems.AddRange(reading.Minted.Select(_ => new EvidenceProblem("a minted look line is not evidence here")));
        if (policy.ObservedNeedsDate)
            problems.AddRange(reading.Observations.Select(EvidenceObservations.DateProblem).OfType<EvidenceProblem>());
        foreach (var reference in reading.References)
            if (await ProblemAsync(reference, policy, probe, ct) is { } problem) problems.Add(problem);
        return problems;
    }

    private static async Task<EvidenceProblem?> ProblemAsync(
        EvidenceReference reference, EvidencePolicy policy, IEvidenceProbe probe, CancellationToken ct)
    {
        if (reference.Qualifier is { } qualifier && policy.UncheckedQualifiers.Contains(qualifier, StringComparer.Ordinal))
            return null;
        if (reference.Qualifier is not null && policy.UnknownQualifierIsProblem)
            return new EvidenceProblem($"the qualifier '{reference.Qualifier}' names no repository this check knows", reference.Path);
        if (reference.Qualifier is null
            && policy.RefusedPathPrefixes.Any(p => reference.Path.StartsWith(p, StringComparison.Ordinal)))
            return new EvidenceProblem("a plan is not evidence — cite what it rests on, or a dated observation", reference.Path);
        // A host-shaped path is probed first: 'mcr.microsoft.com/dotnet/sdk:10.0' is an image tag,
        // not a malformed line list, once the probe says no such file exists.
        if (reference.HasUnparseableLines && !reference.IsHostShaped) return Unparseable(reference);

        return await probe.ProbeAsync(reference.Qualifier, reference.Path, ct) switch
        {
            EvidenceProbeResult.NotAFile when reference.IsHostShaped => null,
            EvidenceProbeResult.NotAFile => new EvidenceProblem("not a file (missing, or a directory)", reference.Path),
            EvidenceProbeResult.File when reference.HasUnparseableLines => Unparseable(reference),
            EvidenceProbeResult.File file => EvidenceLineBounds.Problem(reference, file.LineCount),
            _ => null,
        };
    }

    private static EvidenceProblem Unparseable(EvidenceReference reference) =>
        new($"the lines '{reference.LineText}' do not parse", reference.Path);
}
