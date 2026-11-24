namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dg: the seal fingerprint — a keyed digest over <see cref="PocSealInputs"/>, so a stored one reveals
/// nothing of a short value. Keyed from the master key: without one there is none (<see cref="IsAvailable"/> false).
/// </summary>
public interface IPocSealFingerprint
{
    bool IsAvailable { get; }

    /// <summary>'hmac-sha256:&lt;64 hex&gt;'; an InvalidOperationException without a master key.</summary>
    string Of(PocSealInputs inputs);
}
