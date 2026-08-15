using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0331/p0386: the parsed ticket→repo classifier reply. <see cref="Repos"/> is
/// the classifier's per-repo verdict list (raw names, validated against the
/// run's repo list by <see cref="RepoScopeEvaluator"/>); each entry carries its
/// OWN confidence, so a confident exclusion of one repo survives doubt about
/// another.
/// p0336b: <see cref="Contexts"/> is the optional per-repo affected-CONTEXT map
/// (repo name → context names) — the classifier's finer-grained verdict used to
/// drop a whole sandbox within a kept repo. Null = no context-level verdict, so
/// <see cref="ContextScopeEvaluator"/> keeps ALL contexts of every kept repo.
/// </summary>
/// <param name="Tier">p0341c: a coarse complexity estimate (trivial|small|medium|large)
/// on the SAME classification call — it sizes this run's effective cost cap (bug→small,
/// cross-repo migration→large). Unknown when the model omitted / malformed it, which
/// falls back to the static per-pipeline default (fail-safe). Never load-bearing for
/// correctness — verification is the real judge of done; the tier only sizes a ceiling.</param>
/// <param name="ExpectedChanges">p0384: the optional subset of the kept repos that must
/// CHANGE (vs kept only for inspection). Validated by <see cref="RepoScopeEvaluator"/>
/// against the kept set; the keystone then requires a committed diff per listed repo.
/// Null/empty preserves the keystone's anyCode semantics (fail-open).</param>
/// <param name="Shape">p0413: the SHAPE of the work beside its size — a deterministic
/// transformation, judgement, or mixed — with the one line that says why. It decides how
/// the ticket is CUT (how many phases, how their steps are phrased), never whether a model
/// is involved. Null when the model stated no shape, and the derivation is then told
/// nothing: it cuts exactly as it did before.</param>
public sealed record RepoScopeClassification(
    IReadOnlyList<RepoScopeVerdict> Repos, string? Rationale,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Contexts = null,
    ComplexityTier Tier = ComplexityTier.Unknown,
    IReadOnlyList<string>? ExpectedChanges = null,
    WorkShapeVerdict? Shape = null);
