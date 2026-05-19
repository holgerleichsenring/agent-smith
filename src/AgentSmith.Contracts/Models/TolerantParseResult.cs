using System.Text.Json;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Outcome of an <see cref="AgentSmith.Contracts.Services.ITolerantJsonParser"/>
/// parse. Document is null when even tolerant recovery failed — callers decide
/// whether partial recovery (Diagnostics non-empty, Document non-null) is
/// acceptable or whether the run should fail. The parser itself does not
/// make that judgement.
/// </summary>
public sealed record TolerantParseResult(
    JsonDocument? Document,
    IReadOnlyList<TolerantParseDiagnostic> Diagnostics);

/// <summary>
/// One recovery shape the parser applied (or the failure that ended recovery).
/// Kind names the shape (markdown-fence stripping, prose stripping, resilient
/// brace-counted fallback) and Detail carries a short human-readable explanation
/// — exception message or recovered-count.
/// </summary>
public sealed record TolerantParseDiagnostic(
    TolerantRecoveryKind Kind,
    string Detail);

/// <summary>
/// Recovery shape names. Counter labels live off this enum so operators can
/// see which LLM is misbehaving most without invasive logging.
/// </summary>
public enum TolerantRecoveryKind
{
    /// <summary>Direct parse succeeded — no recovery needed.</summary>
    None,
    /// <summary>```json fences (or generic triple-backtick) wrapped the JSON; stripped.</summary>
    FencesStripped,
    /// <summary>Prose surrounded the JSON; the outermost { ... } or [ ... ] span was extracted.</summary>
    JsonExtracted,
    /// <summary>Brace-counted resilient extraction salvaged complete object literals from a truncated array.</summary>
    ResilientFallback,
    /// <summary>Even tolerant recovery failed — JsonException surfaced through.</summary>
    Failed,
}
