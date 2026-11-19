namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413a: what the one scope call came back with. 2026-11-19-6b0bb: only what a run uses — the ticket estimate and
/// the security refusal; the per-repository verdicts, contexts and expected changes it once returned narrowed the
/// run (gone since 2026-11-18-9c1cb) or fed the derivation (gone since 2026-11-19-6b0ba).
/// </summary>
/// <param name="Estimate">The ticket's size and shape — <see cref="ScopeEstimate.None"/> when the reply stated neither.</param>
/// <param name="Error">Why there is no reply at all — the call failed; null when a reply came back.</param>
/// <param name="Refusal">The reply's judgement that the ticket demands something that must not be done; null when it
/// stated no objection.</param>
/// <param name="CallFailed">2026-10-12-6bfae: the call itself failed — no reply exists.</param>
public sealed record ScopeClassificationResult(
    ScopeEstimate Estimate, string? Error, ScopeRefusal? Refusal = null, bool CallFailed = false);
