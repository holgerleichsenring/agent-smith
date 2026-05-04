# Triage

Triage is the LLM-driven step that decides, per ticket, which skills participate in which roles in which pipeline phases. It runs once per pipeline run, produces single-line JSON, and emits the resulting `SkillRound` / `FilterRound` commands for the framework to dispatch.

For Discussion-type pipelines (`legal-analysis`, `mad-discussion`, `init-project`, `skill-manager`, `autonomous`), triage falls back to a legacy LLM strategy that picks Lead + Participants from available roles — phases don't apply.

## Roles

| Role | Output | What the skill does |
|---|---|---|
| `Lead` | Plan | Sets the plan that reviewers compare against. One per phase. |
| `Analyst` | List of observations | Contributes perspective; no veto. |
| `Reviewer` | List of observations | Compares actual code/diff against the lead's plan. Evidence-required. |
| `Filter` | Reduced list or single artifact | Reduces a list (drops duplicates, false positives) or synthesizes the final report. |

## Phases

Structured pipelines (`fix-bug`, `add-feature`, `security-scan`, `api-security-scan`) declare three phases: `Plan`, `Review`, `Final`.

`fix-bug` and `add-feature` run an `AgenticExecute` step between Plan and Review — the developer agent does the actual code-writing work using the plan from Plan-phase Lead. Review-phase Reviewers then check the resulting diff against the same plan via the `{{plan}}` template token.

`security-scan` and `api-security-scan` have no `AgenticExecute` (read-only scans); the phases run back-to-back.

## Output schema

Single-line JSON:

```json
{"phases":{"Plan":{"lead":"architect","analysts":["tester"],"reviewers":[],"filter":null},"Review":{"lead":null,"analysts":[],"reviewers":["architect"],"filter":null},"Final":{"lead":null,"analysts":[],"reviewers":[],"filter":"reducer"}},"confidence":85,"rationale":"lead=architect:auth-port;analyst=tester:has-tests;-dba:no-db-changes;"}
```

Validation has two layers:

1. **Structural** — rationale ≤ 300 chars; no newlines in JSON.
2. **Semantic** — every assigned skill supports the assigned role (`SkillIndexEntry.RolesSupported`); every rationale token references a key declared in the cited skill's `activation` or `role_assignment`.

A first-attempt failure triggers exactly one retry with a stricter system reminder. A second failure throws.

## Rationale token grammar

Tokens are separated by `;`. Two forms:

- Positive: `<role>=<skill>:<key>;` — "I picked `<skill>` as `<role>` because of activation/role_assignment key `<key>`."
- Negative: `-<skill>:<key>;` — "I rejected `<skill>` because of `<key>`."

Roles are `lead`, `analyst`, `reviewer`, `filter`. Keys must already exist in the cited skill's `activation.positive`, `activation.negative`, or `role_assignment.<role>.{positive,negative}` lists. Invented keys are rejected at validation.

## Confidence

The LLM emits a confidence score (0–100). Below 70, the framework auto-downgrades any `Blocking=true` observation a downstream skill produces to `Blocking=false`, and logs the downgrade. The triage step itself proceeds — confidence < 50 is a signal but not a stop condition (operator policy may differ).

## Ticket label overrides

Two ticket-label patterns are hard overrides applied to the LLM's output before commands are emitted:

- `agent-smith:skip:<skill>` — drops `<skill>` from all roles in all phases.
- `agent-smith:no-test-adaption` — drops the `tester` skill from all roles.

The LLM is told about these patterns in the system prompt for context; the framework strips the listed skills regardless of what the LLM emits. Operators can use them to disable a noisy skill on a specific ticket without editing skill configuration.

## Project concept vocabulary

`ProjectMapExcerptBuilder` reads `.agentsmith/project-map.json` (produced by p0110b's `AnalyzeProjectHandler`) plus a `ConceptVocabulary` and projects them onto a narrow `ProjectMapExcerpt` that's fed to the triage prompt. Concept matching happens in the builder via substring match against framework names, primary language, and module paths — the LLM sees a prefiltered concept list, not the raw project map. See [concept vocabulary](../configuration/concept-vocabulary.md) for the file format.

## Plan artifact threading

After the Plan phase, the Lead skill's observations are stored in pipeline context as a `PlanArtifact`. Review-phase skills with a `{{plan}}` placeholder in their `## as_reviewer` body get it substituted at prompt-build time. Reviewers without a Plan-phase lead in the same run see `(no plan provided)` and run as generic reviewers.
