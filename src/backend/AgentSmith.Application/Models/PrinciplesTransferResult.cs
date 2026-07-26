namespace AgentSmith.Application.Models;

/// <summary>
/// p0379: outcome of the deterministic principles transfer that runs before
/// the bootstrap skill call. A non-null <paramref name="Error"/> fails the
/// round loudly — a transfer-mode round must never silently regress to
/// LLM-generated principles.
/// </summary>
public sealed record PrinciplesTransferResult(
    PrinciplesMode Mode,
    string? Error = null);
