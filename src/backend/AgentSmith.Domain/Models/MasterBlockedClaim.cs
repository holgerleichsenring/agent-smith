namespace AgentSmith.Domain.Models;

/// <summary>
/// p0365: the master's optional structured claim that it CANNOT complete a step,
/// with the concrete blocker that stops it. Honoured as terminal only when
/// <paramref name="Blocker"/> is non-empty — a bare "too complex" with no concrete
/// blocker is the can't-side of faking-green and is re-driven, mirroring how a
/// "done" claim is honoured only when an actual diff backs it. Orthogonal to
/// <see cref="MasterVerification"/>: no new VerificationStatus, no keystone change.
/// </summary>
public sealed record MasterBlockedClaim(bool IsBlocked, string? Blocker);
