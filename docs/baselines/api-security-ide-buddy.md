# api-security-scan IDE-buddy reference snapshot

This document records the qualitative bar p0151 aims to hit. It is a checklist of structural properties, not a verbatim transcript.

The reference snapshot was captured by giving the same target (an anonymized .NET reference API, called "Sample" in p0151 artifacts) to a human-operated IDE buddy with full tool access (Read / Grep / ListFiles / Bash / Agent delegation) and recording the observations they produced. p0151 ships when the api-security-scan pipeline produces observations meeting these structural properties on the same target.

## Structural anchors the reference snapshot relied on

- **Permission cache service:** scoped DI lifetime (correctly bounded to request). The cache class is registered as `Scoped`, not `Singleton`, so cross-request data leakage is not a concern. The IDE buddy verified this by reading the DI registration file directly.
- **JWT validation:** the target uses a managed identity library (`AddMicrosoftIdentityWebApi(...)` or equivalent). No `TokenValidationParameters` are customised at the call site. Defaults are secure (`ValidateIssuer = true`, `ValidateAudience = true`, `ValidateLifetime = true`). Any observation claiming weak JWT validation on this target is hallucinated.
- **Raw SQL:** limited hits in a database migration utility (not in the API request path). Both hits are parameterless static SQL strings — `ExecuteSqlRawAsync(const)` — not user-input-driven. Severity is at most informational; flagging them as injection would be a false positive.
- **Dockerfile:** structural review for non-root user, healthcheck, no build-arg secrets. Specific findings depend on the target's Dockerfile content.
- **Configuration files:** review for non-placeholder secrets in `appsettings*.json`, pipelines, Dockerfile. The reference target carries only template placeholders (no committed real credentials).
- **Controllers:** comprehensive `[Authorize]` coverage at the class level. State-changing actions are gated either by class-level auth or by per-action `[Authorize(Roles=...)]` attributes. Any controller observation claiming missing authorization should cite a specific controller file:line and an attribute that is genuinely absent.

## Acceptance criteria for p0151h

These criteria are applied to a re-run of `api-security-scan` against the same target after all p0151a-g changes have merged:

1. **Every observation carries a verifiable anchor.** Either `file` + `start_line` (for `analyzed_from_source`), or `api_path` (for swagger / endpoint-anchored claims), or `schema_name` (for swagger schema claims), or a scanner template id in the description (for scanner correlations). The `AnchoringVerifier` enforces this at output-render time; pass/fail surfaces in the operator-facing summary.
2. **No `analyzed_from_source` observation without `file` ∈ ReadSet.** The `SourceAnchorValidator` (p0151b) enforces this mechanically; halluciniated source-anchored observations cannot pass the gate.
3. **The pre-fix JWT halluciniation does not recur.** The `jwt-validation-judge` skill's drop-if contract (p0151f) ensures the skill emits `[]` when the target uses a managed identity library. Verification: search the final observations for descriptions mentioning weak issuer / audience / lifetime validation; absence is the success condition.
4. **`tool_calls > 0` for every skill round whose tool-policy returns a non-empty `ToolSet`.** Observable in the `skill_call_trace` log lines (p0151a). Rounds with empty tool-policy (e.g. filter rounds per p0148) are excluded.
5. **Pipeline-agnostic sample.** A run of `fix-bug` and `security-scan` against fixture inputs shows the same source-anchored discipline on `analyzed_from_source` observations from those pipelines' judges. If those pipelines do not benefit from the infrastructure work, p0151's pipeline-agnostic thesis is wrong.

## Anti-checklist — observations that should NOT appear on this target

- "Missing rate limiting across endpoints" with no `api_path` and no specific endpoint examples.
- "Unbounded string inputs" with no cited schema and no specific endpoint examples.
- "JWT expiry too long" / "no issuer check" / "no audience check" against this target's identity-library stack.
- "Raw SQL injection" pointing at the migration utility's parameterless static SQL.
- Any `analyzed_from_source` observation citing a file that is not in the skill call's trace ReadSet.

These are the patterns the pre-p0151 baseline produced. Their absence in the re-run is the success metric.
