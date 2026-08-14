namespace AgentSmith.Domain.Models;

/// <summary>
/// p0421: whether the run delivered what its ratified criteria asked for, and — when it
/// did not — exactly which criterion is outstanding.
/// <para>
/// The failure reason is never generic. Its predecessor answered "this fix/feature run
/// produced no code changes", which is true of every resumed branch and tells an
/// operator nothing about what is actually missing.
/// </para>
/// </summary>
public sealed record DeliveryVerdict(bool Satisfied, string? FailureReason = null)
{
    public static DeliveryVerdict Ok() => new(true);

    public static DeliveryVerdict Fail(string reason) => new(false, reason);
}
