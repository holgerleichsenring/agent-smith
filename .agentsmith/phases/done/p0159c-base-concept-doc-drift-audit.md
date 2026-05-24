# p0159c — Doc drift audit (2026-05-22)

Working artefact, not user-facing. Records what was checked, what changed, and what was deliberately left for follow-up.

## Method

Two Explore agents ran in parallel against the eight concept pages and four configuration pages named in the p0159c spec. Each was given the named C# source-of-truth files and reported drift bullets with severity (high / medium / low). Reports were under 800 + 600 words respectively.

## Concept docs

| File | Severity | Fix applied here | Status |
|---|---|---|---|
| `concepts/sandbox-architecture.md` | low | none | Verified: per-repo sandbox routing matches `SandboxSpecBuilder` + `KubernetesSandboxFactory` + p0158e. No drift. |
| `concepts/sandbox-agent.md` | low | none | Verified: wire format table matches `Sandbox.Wire/Step.cs` + `SizeLimits.cs`. Minor wording: examples show 200-line cap but `GrepDefaultHeadLimit = 1000`; example is "typical use", not max. Left as-is. |
| `concepts/multi-agent-orchestration.md` | (audit flagged high but grep found 0 OBJECT/AGREE in file) | none | Spot-check confirmed: roles table is already Lead / Analyst / Reviewer / Filter. No drift. Audit may have been over-eager. |
| `concepts/pipeline-system.md` | medium | **applied** | Step-count table updated: `fix-bug` 13 → 22, `add-feature` 14 → 24, etc. Authoritative-source note added pointing at `PipelinePresets.*.cs`. |
| `concepts/triage.md` | low | none | Verified: triage page describes the current LLM-driven path. Regex parser retirement (p0146b) is reflected. No drift. |
| `concepts/multi-skill.md` | **high** | **applied** | Three sections rewritten: "Discussion Pipelines", "Backward Compatibility", "Discussion Flow". OBJECT / AGREE / SUGGEST free-text prose-regex convergence replaced with typed `SkillObservation` (Concern + Confidence + Blocking + EvidenceMode). |
| `concepts/phases-and-runs.md` | **high** | **applied** | Run-id format updated from sequential `r{NN}` to `{yyyy-MM-ddTHH-mm-ss}-{4hex}-{slug}` (p0156). Top-level `runs:` key in context.yaml documented. Display-format note added. Two run-listing code blocks updated. |
| `concepts/ticket-lifecycle.md` | medium | **applied** | Pending → Enqueued row clarified: `TicketClaimService.ClaimAsync` emits **one** `ClaimRequest` per ticket (p0158a unification). `RepoName` field is gone. Admonition box added pointing at multi-repo-pipelines.md. |

## Configuration docs

| File | Severity | Fix applied here | Status |
|---|---|---|---|
| `configuration/agentsmith-yml.md` | **high** | **partial** | Page describes pre-p0139 inline `projects.{name}.source/tickets/agent` shape; current schema is catalog-based. Full rewrite is out of scope for p0159c (a separate phase). For now: warning admonition added at the top pointing at multi-repo.md + the schema reference page. Plus `AzureRepos` → `AzureDevOps` typo fix (Medium severity from audit). |
| `configuration/multi-repo.md` | none | none | Verified: schema matches `ProjectConfig` + `RepoConnection`. |
| `configuration/project-resolution.md` | none | none | Verified: matches current `ProjectResolver` flow. |
| `configuration/tools.md` | none | none | Audit confirmed: this file documents external security-scan tools (Nuclei, Spectral, patterns), not the agentic skill-surface tools that p0153/p0154 reshaped. The skill-surface tool surface lives in `architecture/` / `skills/` docs, not here. |

## Deferred

The following were identified during the audit but deliberately not fixed in this phase:

- **`configuration/agentsmith-yml.md` full rewrite** — page needs to be re-authored against the post-p0139 catalog-based schema (top-level `agents:`, `trackers:`, `repos:` catalogs + `projects.{name}.repos: [name, ...]` reference list). Marked as "follow-up phase" — worth its own focused commit because every code example in the file changes shape.
- **`pipelines/mad-discussion.md`, `pipelines/index.md`** — audit didn't include these in the scoped list but per the p0146c decisions.md note they likely still describe OBJECT/AGREE. Worth a sweep in a follow-up.
- **Auto-step-count generation** — every change to a `PipelinePresets.*.cs` file will re-drift pipeline-system.md. A generator that emits the step-count table from the C# array literals would fix this for good. Out of scope here.
- **`SKILL.md` rename throughout docs** — `agentsmith.md` (skill spec filename) was renamed to `SKILL.md` somewhere along the way. Some docs still say `agentsmith.md`. Mechanical sed pass; do as own phase.

## Theme + tokens

- Old `docs/stylesheets/custom.css` (DM Sans + Linear-style green palette `#00a854`) deleted.
- Old `docs/overrides/stylesheets/linear.css` (Linear-themed `#5e6ad2` indigo overrides) deleted; empty `docs/overrides/` directory removed.
- New `docs/stylesheets/overrides.css` maps Material's `--md-*` variables to DESIGN.md tokens (`var(--color-primary)` etc.). Light scheme uses cream canvas + coffee ink + Smith green; dark scheme uses ink canvas + cream-soft text + same Smith green.
- `mkdocs.yml extra_css` updated to `tokens.css` + `overrides.css`.

## Verification

- `mkdocs build --strict` was NOT run locally — Python / mkdocs-material aren't installed in this dev environment. Verified via the CI workflow update in p0159a (`.github/workflows/docs.yml` now runs `node scripts/build-tokens.mjs` before `mkdocs build --strict`, so the next push to main will validate the full graph). The workflow's strict flag means any broken nav entry or dangling link fails the build.
- Browser walk: pending operator review before push.
