using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Sealing;

/// <summary>2026-11-23-933dg: without the secret store there is no master key, so there is no fingerprint.</summary>
public sealed class NoPocSealFingerprint : IPocSealFingerprint
{
    public bool IsAvailable => false;

    public string Of(PocSealInputs inputs) => throw new InvalidOperationException("No master key: no seal fingerprint.");
}
