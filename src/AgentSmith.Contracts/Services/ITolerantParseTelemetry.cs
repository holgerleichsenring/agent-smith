using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Lightweight seam invoked by <see cref="ITolerantJsonParser"/> whenever a
/// recovery shape kicks in. Default implementation is a no-op; operators wire
/// in a counter-emitting implementation (Prometheus, OpenTelemetry) to see
/// which LLM is misbehaving most without invasive logging.
/// </summary>
public interface ITolerantParseTelemetry
{
    /// <summary>
    /// Records that recovery <paramref name="kind"/> was applied while parsing
    /// LLM output. <paramref name="detail"/> is the same string as the matching
    /// <see cref="TolerantParseDiagnostic.Detail"/>; implementations free to
    /// project it into tags or ignore it.
    /// </summary>
    void Record(TolerantRecoveryKind kind, string detail);
}
