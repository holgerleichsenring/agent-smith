# Phase D — Implementation Order

**Purpose:** Sequence and granularity of the implementation phases that turn the model from A/B/C into reality. Visible progress after each sub-phase, no big-bang at the end.
**Prerequisites:** Phase A, B, C.
**No code in this document.** Effort estimates are planning anchors, not commitments.

---

## Overview

Seven implementation phases. Each comparable in size to existing p-phases in the project (1-3 weeks). Order is not arbitrary — the first three phases are prerequisites for the rest.

| # | Phase | Duration | Visible improvement after completion |
|---|---|---|---|
| D1 | Concept vocabulary and run-state | 1-2 weeks | Triage decisions are deterministic and traceable |
| D2 | Tool-loop in skill-round infrastructure | 2-3 weeks | Skills read code instead of guessing — the Bug-18693 class of failures stops |
| D3 | Skill format migration | 1-2 weeks | All skills follow the new single-body format; multi-role files are gone |
| D4 | Plan and Diff schemas, persistence | 1 week | Plans and diffs survive runs as files; replay becomes possible |
| D5 | Verify phase for fix-bug and feature-implementation | 1-2 weeks | Implementation diffs are checked against real code before delivery |
| D6 | init-project pipeline | 1-2 weeks | New repos onboard cleanly; missing context.yaml stops dependent pipelines explicitly |
| D7 | Cleanup and removal of the old model | 1 week | `roles_supported`, `role_assignment`, `activation.positive/negative`, code-map generation are gone |

**Total effort:** 8-13 weeks for one developer working full-time on this stream. Add 30-50% slack for context switching with day-to-day work.

The order is built around three constraints:

1. **D1 must come first** because everything downstream depends on deterministic activation. Without the concept vocabulary, the new triage cannot work even if everything else is in place.
2. **D2 (tool-loop) must come before D5 (Verify)** because Verify is itself a tool-loop user. Building Verify on top of single-shot LLM calls would mean rebuilding it later.
3. **D7 (cleanup) must come last.** Until D6 ships, the old code paths still serve real traffic. Removing them earlier breaks production.

D3 (skill format) and D4 (schemas) can in principle run in parallel, but doing so requires two streams of attention. The default order keeps it sequential.

---

## D1 — Concept Vocabulary and Run-State

**What gets built:**

- `concepts.yaml` schema and loader. Lists all permitted concepts with type, value range, description, writer-name list. Validated at server boot — invalid concept files fail boot loudly.
- `IRunStateConcepts` interface (or similar shape) that lets handlers read and write concept values during a pipeline run. Strict typing per concept; writes that violate the declared type fail at the call site.
- Pre-skill handlers updated to publish concrete concepts. The existing `TryCheckoutSourceHandler`, `LoadSwaggerHandler`, `SpawnNucleiHandler`, etc. each get a concept-publication step at the end. Mapping:
  - `TryCheckoutSourceHandler` → `source_available`, `source_path`
  - `LoadSwaggerHandler` → `swagger_spec_present`, `swagger_path`
  - `SpawnNucleiHandler` → `nuclei_findings_count`
  - `SpawnZapHandler` → `zap_findings_count`
  - `SpawnSpectralHandler` → `spectral_findings_count`
  - `BootstrapCheckHandler` (new) → `context_yaml_present`, `coding_principles_present`
  - Pipeline setup → `pipeline_name` (always)
- Boolean expression evaluator for `activates_when` strings. Pure function over the concept dictionary, no LLM. Operators: `AND`, `OR`, `NOT`, `=`, `>`, `>=`, `<`, `<=`. Parentheses allowed.
- Concept-vocabulary CI check: any handler that writes a concept must declare it in `concepts.yaml`; any expression that reads a concept must reference one declared there. Build fails on mismatch.

**What does not get built yet:** triage rewrite (D2 territory — for now triage continues to read the old activation lists, in parallel with the new system being seeded).

**Visible after D1:**

- Run logs show the concept dictionary at the moment triage runs. A human reading the log can see exactly which concepts were true and reproduce the activation decision by hand.
- The `concepts.yaml` file exists and is the canonical reference for what triage knows.
- Layer 5 (Concepts as facts, not vibes) of the original bug-stack is closed.

**What remains broken after D1:** triage still uses the old activation lists; the new vocabulary is published but not yet consumed for skill selection. That is the next phase.

**Risk and migration notes:**

- Adding concept publication to existing handlers is mechanical but touches many files. Keep the handler changes additive — they should publish concepts *and* preserve the existing handler outputs unchanged. No skill-side change yet.
- The boolean evaluator is the place where subtle parsing bugs hide. Cover it with a focused test pack including operator precedence (`a AND b OR c` parses as `(a AND b) OR c`), short-circuit behavior, and missing-concept default values per type (`false` for bool, `0` for int, first enum value for enums).

**Estimate:** 1-2 weeks. Lower bound assumes the boolean evaluator can be a small recursive-descent parser written in a day or two; upper bound assumes that over several days of integration, more pre-skill handlers turn out to need concept-publication entry points than the eight listed above.

---

## D2 — Tool-Loop in the Skill-Round Infrastructure

**What gets built:**

- Skill-call becomes a loop session, not a single LLM call. New abstraction: a skill call takes a system prompt, an initial user message, and a set of available tools, and returns either a final answer (when the LLM stops calling tools) or a status indicating limit hit / parse fail / runtime fail.
- Tool registry per phase type. `read`, `grep`, `find` for all loop-using skills. `bash` for Implementation and Verify only. `write` for Implementation and Bootstrap only.
- Limits enforced per skill call:
  - `max_tool_calls_per_skill` (30 default, planners and implementers)
  - `max_tool_calls_per_investigator` (10 default, regular investigators)
  - `max_tool_calls_per_verifier` (20 default, Verify investigators)
  - `max_llm_calls_per_skill` (15 default)
  - `max_input_tokens_per_skill_call` (200_000 default)
  - `max_output_tokens_per_skill_call` (16_000 default)
  - `max_seconds_per_skill_call` (300 default)
  - `max_concurrent_skill_calls` (10 default, per pipeline run)
- Five-state outcome per skill call: `ok`, `incomplete`, `failed_parse`, `failed_validation`, `failed_runtime`. Pipeline behavior per state as defined in Phase B.
- Cost tracking adapted to per-loop-session granularity. Existing `PipelineCostTracker` extended; per-call breakdown shows tool-call count, LLM-call count, input/output tokens, wall-clock time.
- `SkillRoundHandlerBase` rewritten so a phase that contains tool-loop skills runs them through the loop runtime. Phases without loop (judges, filters by default) keep their existing single-call path.
- Read-access enforcement: every `read`, `grep`, `find` call checks the path against the skill's read scope. Read scope is "everything git-tracked outside `.gitignore`" by default; `scope_hint` from the frontmatter, if present, is injected into the prompt as a soft guidance. Violations of the hard rules (write outside Implementation/Bootstrap, bash outside Implementation/Verify) are rejected at the tool layer with an error message that goes back into the loop.

**What does not get built yet:** new skill format (D3 territory — for now skills still use the old `as_<role>` body format, with the loop runtime treating any role's body as a single body for loop purposes). Verify phase (D5).

**Visible after D2:**

- A planner skill running on Bug 18693 finds and reads `ApplicationController.cs` before producing a plan, instead of guessing from project_map_excerpt. The 22-file blast radius from the original run cannot happen the same way, because the planner now has the actual code.
- Per-skill cost reports in run results show tool-call counts and a meaningful loop trace.
- Investigators in api-security-scan can verify a finding against the actual code rather than reasoning from finding text alone.

**What remains broken after D2:** skills are still in the old multi-body format; activation is still done by the old triage. The new loop runtime is in place but underutilized.

**Risk and migration notes:**

- This is the largest single phase. The risk is that the loop runtime needs to thread cleanly through `IChatClientFactory`, `IAgenticAnalyzer` patterns, `SandboxToolHost`, and the existing `FunctionInvokingChatClient`. The intent is *not* to replace these — `FunctionInvokingChatClient` from the M.E.AI migration (p0119a) already provides a tool-using loop. The work is to add the limits, the five-state outcome, the read-scope enforcement, and the cost tracking around it.
- Existing skills that don't use tools must keep working without changes. The loop runtime detects "skill produced final answer on first turn" and treats that as `ok` immediately; no extra LLM round-trips for non-tool skills.
- The transition from agentic-analyzer-style providers to a unified loop runtime might surface latent bugs in tool-call serialization across providers (Anthropic vs OpenAI vs Gemini). Schedule integration testing across all four providers.
- Concurrency limit (`max_concurrent_skill_calls`) needs a semaphore at the pipeline level, not at the skill-runtime level. Otherwise it can't enforce a per-run cap.

**Estimate:** 2-3 weeks. The bulk is the loop runtime and limits. Read-scope enforcement and tool registries are the day-2 work after the runtime exists.

---

## D3 — Skill Format Migration

**What gets built:**

- New skill frontmatter as defined in Phase C: `name`, `version`, `role`, `description`, `activates_when`, `output_schema`, `category`, `investigator_mode`, `survey_scope`, `scope_hint`, `block_condition`, `loop`. Validation at skill load per the rules in Phase C.
- New body format: a single Markdown body, no `## as_<role>` sections. Recommended sub-sections (Task / Discipline / Approach / Output Fields) are advisory, not enforced.
- `YamlSkillLoader` rewritten to read the new format. The old multi-body format is supported in parallel for one transition period (skills carrying both `roles_supported` and `role` get warnings; the new fields take precedence). Hard cutover at the end of D3.
- Migration of all 33 existing skills in `agent-smith-skills/skills/`. Where a skill had three roles, it becomes three skills. Where it had one role, it stays one skill with smaller frontmatter. Naming convention for the split: `<original>-<role>` (e.g. `architect-as-lead` becomes `architect-planner`, `architect-as-reviewer` becomes `architect-judge`).
- `category` field becomes mandatory for `investigator_mode: verify_hint` skills. The category list is initially closed: `auth`, `injection`, `secrets`, `iam`, `crypto`, `headers`, `inputs`, `outputs`. Skills with categories outside this list fail validation. Adding a new category is a deliberate vocabulary change.
- Triage rewritten to use the new selection algorithm:
  1. Filter skills by phase role
  2. Filter by `activates_when` over current run-state
  3. If more skills match than `skills_count` allows, pick by specificity (more concept references = more specific; ties broken lexicographically by skill name)
  4. If fewer skills match than the phase needs, escalate to human

**What does not get built yet:** Plan and Diff schemas as separate concepts (D4). Verify phase (D5).

**Visible after D3:**

- A new operator can read a `SKILL.md` file and predict when it activates, what role it plays, and what output to expect, without reading code.
- Skill count grows (probably from 33 to roughly 40-45 after splitting multi-role files), but each individual skill is sharper and shorter.
- Triage selection logs show a clean trail: "skill X matched because activation expression Y evaluated to true, picked over Z because more specific."
- Layer 1 (skill load failures from missing `as_<role>`) is structurally gone. There are no `as_<role>` sections to be missing.

**What remains broken after D3:** skill outputs still use the existing observation/free-text patterns. The strict Plan and Diff schemas don't exist yet. Pipelines still run fix-bug without Verify.

**Risk and migration notes:**

- The 33-skills migration is the busy part. Plan a skill-by-skill review; not all of them split cleanly into single-role skills. Some may turn out to be a single role wearing different hats — those collapse to one skill with a single body. Others are genuinely multi-role and split into two or three.
- The `category` enum needs review with whoever owns the api-security-scan and security-scan domain knowledge. The starting set above is an educated guess; the actual list may differ.
- Triage rewrite is conceptually simple but practically delicate. Existing test pack for triage (under `Triage/StructuredTriageStrategyTests.cs`) needs adaptation. Plan for that adaptation, not just the new tests.
- During the migration window, both the old and new triage paths exist. Pick one project as the canary (suggestion: agent-smith itself) and keep external projects on the old path until the new path is proven on agent-smith for at least a week.

**Estimate:** 1-2 weeks. The skill-by-skill migration is the variable part. Tooling helps: a small migration script that reads old SKILL.md, splits roles, writes new files, and reports unresolved cases for human review can save days.

---

## D4 — Plan and Diff Schemas, Persistence

**What gets built:**

- JSON schemas for `Plan` and `Diff` as defined in Phase C. Plan is the planner's output, Diff is the implementer's output.
- Schema validation at phase boundaries. Output that doesn't match its schema returns `failed_validation`; the loop runtime retries once with the validation error as feedback, then escalates per Phase B.
- Persistence: every Plan and every Diff is written to `.agentsmith/runs/<run-id>/plan.json` and `.agentsmith/runs/<run-id>/diff.json`. Pipeline storage (Redis or in-memory) holds the same content for the running phases. On pipeline end, file persists, in-memory storage is discarded.
- `open_questions` mechanism: when a planner returns `status: needs_user_input`, the pipeline writes the plan back to the ticket along with the open questions. The ticket comment includes question IDs (`q1`, `q2`, …) that the human answers in a structured form ("Q1: option A, Q2: don't change UPDATE/DELETE"). The next planner run reads the answers as additional input.
- Bootstrap-output schema as a fourth variant: confirmation message, `status: complete | needs_user_input`, list of files written. Validates that `context.yaml` and `coding-principles.md` exist after the call and parse correctly.
- Run-state visibility rules implemented per Phase B: data flows are opt-in, declared by pipeline implementations as directed edges. A Review judge gets the Plan; an Implementation producer gets the Plan; a Verify investigator gets the Diff plus build/test outputs. Anything not declared is rejected at the read layer.

**What does not get built yet:** Verify phase (D5).

**Visible after D4:**

- Replaying a run becomes possible: take the persisted Plan from `.agentsmith/runs/...`, feed it back into the implementation phase manually, and reproduce the diff. Useful for debugging and CI integration.
- `open_questions` flow gives operators a real escape hatch: rather than the pipeline guessing on ambiguous tickets, it asks. Bug 18693 with a human-clarified scope ("only ApplicationController.cs, only Create/Get") would have benefited even from the pre-D2 planner if this mechanism existed.
- Schema-validated outputs make downstream phases less defensive. The Verify phase (next) can rely on the Diff being well-formed.

**What remains broken after D4:** Verify phase doesn't exist; implementations still ship without an independent post-implementation check. Old pipelines (mad-discussion, the scan pipelines) work but don't yet take advantage of the schemas.

**Risk and migration notes:**

- The `open_questions` round-trip requires ticket-platform integration on both write (writeback with question IDs) and read (re-trigger the pipeline with answers). Existing webhook code from p0084 has the trigger mechanism; the structured-answer parsing is new.
- Persistence under `.agentsmith/runs/` may grow large for repos with many runs. Add a retention policy in `agentsmith.yml` (e.g. keep last N runs per ticket, or last N days). Don't add this in D4 unless the disk-fill risk is real; flag it as a follow-up.
- Schemas should be defined as JSON Schema (or equivalent) and compiled into the validation layer at boot time, not parsed at every call. This is performance-relevant for high-volume pipelines.

**Estimate:** 1 week. The schemas themselves are small. Persistence is mechanical. The `open_questions` round-trip is the one variable item — depends on ticket-platform quirks (Jira's comment format is different from GitHub's, etc.).

---

## D5 — Verify Phase for fix-bug and feature-implementation

**What gets built:**

- Verify phase added to the fix-bug and feature-implementation pipeline implementations. Phase runs after Implementation, before delivery.
- VerifyDiff investigators: a new investigator mode (`investigator_mode: verify_diff`) that takes the Diff as input, runs `bash` for build/test, and produces observations. Frontmatter validation extended to support the new mode.
- Initial Verify investigators:
  - `build-verifier` — runs the project's build (driven by ProjectMap conventions), reports failures
  - `test-verifier` — runs the project's tests, reports failures and parses test output
  - `architecture-verifier` — reads the Diff and `coding-principles.md`, reports diff-level violations of declared principles
  - `scope-verifier` — reads the Plan and the Diff, reports files changed that weren't in the Plan's `scope.files`
- Block behavior per Phase A: any blocking observation from a Verify investigator triggers re-implementation with the original Plan plus all Verify notes as additional input. A second blocking observation in the second round escalates to the ticket.
- Pipeline cost cap awareness: the second-round Verify uses the same limits as the first. No bonus.

**What does not get built yet:** init-project pipeline (D6).

**Visible after D5:**

- A Bug-18693-class run cannot ship a wrong implementation. The 22-file blast radius would be flagged by `scope-verifier` ("changes touch 22 files; Plan declared scope.files: [ApplicationController.cs]"). Build/test runs catch other regressions.
- Operators see Verify reports in the run output: which checks ran, what they found, why the diff was accepted or sent back.
- Cost per Bug-fix run goes up by roughly 20-40% (Verify is real work). Per Buddy's discussion: this is acceptable for the quality gain.

**What remains broken after D5:** init-project pipeline doesn't exist; new repos still onboard through the legacy bootstrap mechanism. Old patterns (`roles_supported`, `code-map.yaml` generation) still exist parallel to the new system.

**Risk and migration notes:**

- The four initial Verify investigators are a starting set. After D5 ships, run it on real bug-fix tickets for two to three weeks; the gaps in Verify become obvious through real failures (e.g., "should have caught X, didn't"). Plan one follow-up cycle to add or sharpen Verify investigators based on what production reveals.
- `architecture-verifier` is the trickiest: it needs to interpret `coding-principles.md` (free-form Markdown) into something checkable. Initial implementation can be conservative — "look for explicit naming conventions like `_camelCase` and `IPrefix`, flag obvious violations." More sophisticated style-checking is a later refinement, not a D5 deliverable.
- Re-implementation rounds need careful instrumentation. Track how often a re-implementation fixes the issue versus how often it produces a different, equally-flawed diff. If the latter dominates, the prompt for the implementer needs work.

**Estimate:** 1-2 weeks. Build/test verifiers are mostly orchestration. Architecture and scope verifiers are simple. The integration into the fix-bug and feature-implementation pipelines (block flow, re-implementation, escalation) is the substantive work.

---

## D6 — init-project Pipeline

**What gets built:**

- New pipeline implementation `init-project`. Single phase: `bootstrap`. One producer skill with full tool-loop, writes `context.yaml` and `coding-principles.md` to the repo root.
- Initial bootstrap skills, one per language family:
  - `csharp-bootstrap` — for .NET projects (detects via `.csproj` files)
  - `node-bootstrap` — for Node.js projects (detects via `package.json`)
  - `python-bootstrap` — for Python projects (detects via `pyproject.toml` or `requirements.txt`)
  - `generic-bootstrap` — fallback for unmatched repos
- `BootstrapCheckHandler` becomes the standard pre-skill handler for all non-init pipelines. It reads the repo for `context.yaml` and `coding-principles.md` and publishes `context_yaml_present` and `coding_principles_present` concepts. If either is false, the pipeline ends immediately with a structured error message: "run init-project first".
- The init-project pipeline does not check those concepts itself — it's allowed to run on a repo without them. That's the whole point.
- Bootstrap skills can ask `open_questions` if the repo content is ambiguous (e.g., "I see C# and Python files — which is the primary language?"). Same mechanism as planners.
- Removal of the old auto-bootstrap path. The `BootstrapProjectHandler` from p0022 stops generating files automatically inside other pipelines. Bootstrap is now an explicit, ticket-able pipeline that runs once.

**What does not get built yet:** the cleanup phase (D7) — old code paths remain in place during D6.

**Visible after D6:**

- A new repo onboards through `init-project` as a first-class pipeline run. The result is a deliberate `context.yaml` and `coding-principles.md`, reviewed by the human before any fix-bug or feature-implementation runs.
- Existing repos that already have `context.yaml` and `coding-principles.md` are unaffected. Pipelines run as before.
- A repo without bootstrap files attempting fix-bug now fails fast with a clear message, rather than running with degraded context.

**What remains broken after D6:** the old `roles_supported`, `role_assignment`, `activation.positive/negative` fields still exist in the codebase as legacy paths. Skill loaders and triage have dead branches that handle the legacy format. Same for code-map generation.

**Risk and migration notes:**

- Removing the auto-bootstrap from existing pipelines is a behavior change. Existing projects that relied on this (any project that didn't run init-project explicitly) will break on their next run. Communicate this clearly: a one-time `init-project` run is required for every project that doesn't already have the files in place.
- The four bootstrap skills can start small. The C# one matters most because it serves agent-smith itself. The others can be skeletal at D6 and grow over time.
- The `generic-bootstrap` fallback is the safety net but is also the place where output quality is hardest to predict. Limit its scope to the absolute minimum content that downstream pipelines need (project name, language hint, "see human for details" placeholders) rather than trying to do full discovery for unknown languages.

**Estimate:** 1-2 weeks. Pipeline plumbing is straightforward; the four bootstrap skills are the variable bit. C# and Node take a few days each; Python and generic can be lighter.

---

## D7 — Cleanup and Removal of the Old Model

**What gets removed:**

- `roles_supported` field in skill frontmatter. Loader stops reading it.
- `role_assignment` field in skill frontmatter. Loader stops reading it.
- `activation.positive` / `activation.negative` lists. Loader stops reading them.
- All `## as_<role>` body sections. Skills are single-body now.
- Multi-body parsing in `YamlSkillLoader`. Reduces to single-body parsing.
- `CodeMapGenerator` and the `code-map.yaml` artifact. Pipelines no longer load it. The relevant code paths in skills that consumed code-map (a small number after the migration) read context.yaml and use the tool-loop to discover code instead.
- `BootstrapProjectHandler` (the old auto-bootstrap path). Replaced by D6's explicit init-project pipeline.
- `LegacyTriageStrategy` from p0111c. Only `StructuredTriageStrategy` remains, and it's now activation-expression-based.
- Concept-vocabulary's transition warnings (the ones added in D3 for skills carrying both old and new fields). Skills must be fully migrated by this point.
- Validation rules that cross-checked old and new formats. Validators reduce to checking the new format only.

**What does not get removed:** observation schema (kept), pre-filter mechanism (kept), pipeline framework around Command/Handler (kept), all the work from p0001-p0124 that's not specifically tied to the old skill model.

**Visible after D7:**

- The codebase is smaller. Estimated reduction: roughly 1500-2500 lines of code from the cleanup, mostly in `YamlSkillLoader`, the old triage strategies, and the legacy field handling.
- New developers reading the code see one model, not two.
- Layer 1, 2, 3, 4 of the original bug-stack are structurally closed. Layer 5 was closed in D1.
- The agent-smith codebase reflects the architecture in A/B/C without legacy ballast.

**Risk and migration notes:**

- This phase is the most dangerous in terms of breaking things, because it removes safety nets. Every previous phase preserved old behavior in parallel; D7 removes that parallelism.
- Run the full pipeline test suite (all tests, not just unit) before merging D7. Run a real ticket through fix-bug, feature-implementation, mad-discussion, and both scan pipelines. If any pipeline run fails, the cleanup is incomplete.
- Code-map removal is a minor behavior change for existing users who consumed the code-map via the knowledge-base feature (p0061). That feature should be rechecked: does it still work without code-map.yaml? Quick fix: knowledge-base falls back to context.yaml + recent run results, which is in fact what humans want to query against anyway.

**Estimate:** 1 week. Mostly deletions and consequent test fixes. Don't underestimate the test fixes — when 30+ tests reference legacy fields, fixing them all takes a day or two even if mechanical.

---

## Critical Path and What Can Run in Parallel

The strict critical path is **D1 → D2 → (D3, D4 parallelizable) → D5 → D6 → D7**.

- D1 and D2 must be sequential. D2 depends on the concept dictionary D1 creates, even though it's the loop runtime that matters.
- D3 and D4 can run in parallel if there are two streams of work. D3 (skill format) is mostly skill files and the loader; D4 (schemas + persistence) is mostly pipeline-side code. They touch disjoint parts of the codebase.
- D5 needs both D3 (because Verify investigators are skills in the new format) and D4 (because Verify reads the Diff schema). So D5 must wait until both are done.
- D6 needs D3 (bootstrap skills are skills in the new format) and D4 (`open_questions` in bootstrap output). D5 is not a hard prerequisite; D6 could in principle ship before D5. The recommended order keeps D5 first because it has higher visible-quality impact for the average user.
- D7 needs everything else done.

Parallel execution of D3 and D4 saves roughly one week. Whether to do it depends on whether two streams of attention are available.

---

## Acceptance Criteria per Phase (Quick Reference)

| Phase | Done when… |
|---|---|
| D1 | `concepts.yaml` exists, all listed handlers publish their concepts, boolean evaluator passes its test pack, run logs show concept dictionary at triage time |
| D2 | A planner skill on Bug-18693 reads `ApplicationController.cs` via tool-loop and the resulting plan references actual file content; run reports show per-skill tool-call counts |
| D3 | All 33 (or ~40 after splits) skills have new frontmatter; old triage path is removed; new specificity-based triage selection is the default |
| D4 | Plan and Diff schemas validate at phase boundaries; persisted plans/diffs survive runs; `open_questions` round-trip works on at least one ticket platform end-to-end |
| D5 | fix-bug pipeline includes Verify; a deliberately-broken implementation is caught by `build-verifier` or `test-verifier` and re-implemented |
| D6 | A repo without bootstrap files fails fast on fix-bug; init-project pipeline produces working `context.yaml` and `coding-principles.md` for at least the four supported language families |
| D7 | No reference to `roles_supported`, `role_assignment`, `activation.positive/negative`, `CodeMapGenerator`, or `LegacyTriageStrategy` remains in the codebase; all pipelines run end-to-end on real tickets |

---

## What Phase D Does Not Cover

- **autonomous pipeline migration** — Agent Smith writing its own tickets (currently p0057c). It's a separate concern that should follow the cleanup; flagged as a future phase, no impact on D1-D7.
- **PR review iteration** (planned p0025) — same kind of pipeline as fix-bug but with an existing PR as starting state. Once D1-D7 are in place, this becomes a small extension.
- **Multi-repo support** (planned p0023) — orthogonal to the model change. Should be approached independently after D7 ships.
- **Migration of skill content quality** — the existing skills, even after D3's format migration, will benefit from prompt improvements (anti-rationalization sections, tightened body discipline, etc.). That's a content-quality stream, not a model stream. Worth running after D5 when Verify can catch quality regressions.
- **Performance and scaling work** — the current architecture is scaled for single-repo, low-concurrency use. Real production scaling (rate-limiting strategy, parallel pipeline runs across many tickets) is a separate concern.

---

## Risk Concentration Map

If something goes wrong, where is it most likely to bite?

- **D2 is the highest-risk phase.** Tool-loop infrastructure touches every skill execution path. Plan for a real testing pass with all five LLM providers. Budget extra time at the end of D2 specifically for cross-provider integration bugs.
- **D5's `architecture-verifier`** is the most ambiguous deliverable. Define what it should and shouldn't catch before starting; otherwise it will scope-creep.
- **D6's `generic-bootstrap`** is hard to make good without seeing real cases. Ship a minimal version, plan to refine after seeing real generic-language repos.
- **D7's test-suite churn** can stretch the cleanup phase beyond a week if not anticipated. The legacy-field references in tests are the largest mechanical cost.

Other phases (D1, D3, D4) are mechanically larger but have lower risk concentration.

---

## How This Document Is Used

The IDE buddy reads this as the source of truth for *which* phases exist and *what they contain*. Each phase becomes its own p-numbered phase YAML in the project (e.g., `p0125-concept-vocabulary.yaml` through `p0131-cleanup.yaml`) following the existing convention. This document does not prescribe the phase YAML format — that's owned by the IDE buddy and the existing project conventions.

Phase YAMLs may break a single D-phase into multiple p-phases if granularity demands it. For example, D3 (skill format migration) might become p0127a (loader rewrite) and p0127b (skill content migration) if the work splits naturally.

The acceptance criteria above are the definition of done for each D-phase. Sub-phases under each D-phase are free to add their own intermediate criteria, as long as the parent's criterion is met when all children are.

---

## Closing Note

The total estimate (8-13 weeks) assumes one developer in continuous work. Reality is rarely that. With a 30-50% slack factor for context switching, day-to-day work, and the inevitable spec corrections that emerge once code starts being written, plan for **3-5 calendar months** to D7 complete.

The first measurable quality improvement lands at the end of D2 — that's roughly 4-6 weeks in. Bug-18693-class failures stop happening cleanly. If quality improvement before that point is necessary, the answer is not to compress D1+D2 (they're prerequisite) but to consider a temporary mitigation outside this implementation order: a pre-flight check on existing fix-bug runs that flags ticket-vs-diff size mismatches, for instance. That mitigation is not in scope for D1-D7; mention it here so it's visible as an option.