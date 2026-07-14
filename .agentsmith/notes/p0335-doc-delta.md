# p0335 — doc delta table (p0162..p0333c + the p0324-p0329 batch)

Derived from the done-phase specs in `.agentsmith/phases/done/` and the decision
YAMLs in `.agentsmith/decisions/` — not from re-reading the codebase. Every phase
in range appears exactly once. "Target doc page" is where the capability is (now)
documented; internal-only phases carry a one-line justification instead.

Orchestrator ruling for this phase: p0324-p0329 are BUILT on the same branch and
ship in the same PR as this docs sweep, so they are documented as shipped
capabilities (amending the spec's original "planned phases are not documented"
decision).

Legend for status: `done` = documented by p0335 (or already covered),
`covered` = the existing page already described it, `archive` = historical page kept.

## p0162 — p0246g

| Phase | User-visible? | Target doc page | Status / justification |
|---|---|---|---|
| p0162 | no | — | pure move/rename: .NET backend into src/backend/, zero behaviour change |
| p0167a | yes | reference/pipelines/pr-review.md (NEW) | new `pr-review` pipeline preset: agent reads a PR diff, dispatches review skills, emits findings — done |
| p0167b | yes | reference/pipelines/pr-review.md (NEW) | four PR-review skills over the PR diff emitting PR-line-level findings — done |
| p0167c | yes | reference/pipelines/pr-review.md (NEW) | pr-review findings rendered as PR comments, overwrite-on-push re-review — done |
| p0169 | no | — | dashboard toolchain bootstrap only, no user feature |
| p0169a | yes | reference/operations/dashboard.md (NEW) | job board: job cards + run detail — done |
| p0169b | yes | reference/operations/dashboard.md (NEW) | live tailing of in-progress jobs — done |
| p0169c | yes | reference/operations/dashboard.md (NEW) | tool-call trail + skill-round timeline — done (superseded by p0183 tree) |
| p0169d | yes | reference/operations/dashboard.md (NEW) | sandbox terminal-session recordings (optional) — done |
| p0169e | no | — | event-sourcing backbone only, no UI shift |
| p0169f | yes | reference/operations/dashboard.md (NEW) | topology view — done (superseded by p0183/p0231 timeline) |
| p0169g | yes | reference/operations/dashboard.md (NEW) | filter rail / event visibility levels — done |
| p0169h | yes | reference/operations/dashboard.md (NEW) | post-run trail tree — done (superseded by p0183) |
| p0169j-a | yes | reference/operations/dashboard.md (NEW) | event stream retained 24h — done |
| p0169j-b | yes | reference/operations/dashboard.md (NEW) | activity tab — done (superseded by p0183 timeline) |
| p0169j-b1 | no | — | backend typed failure-reason event fields, producer prerequisite |
| p0169j-c | yes | reference/operations/dashboard.md (NEW) | result tab renders result.md — done |
| p0169j-d | yes | reference/operations/dashboard.md (NEW) | topology graph — done (superseded) |
| p0173a | no | — | system-event backbone pipe only |
| p0173b | no | — | poller/webhook system-event producers, UI in p0173d |
| p0173c | no | — | chat/config/catalog system-event producers, UI in p0173d |
| p0173d | yes | reference/operations/dashboard.md (NEW) | /system page: status, activity, trigger log ("why didn't this ticket trigger") — done |
| p0173e | no | — | typed message contracts + schema-evolution policy (docs/event-schema-policy.md relocated to reference/architecture/) |
| p0173f | yes | reference/operations/dashboard.md (NEW) | filter-rail dimensions, typed per-event renderers — done |
| p0174 | yes | reference/operations/dashboard.md (NEW) | per-pull-cycle aggregation in system view; DESIGN.md tokens — done |
| p0176a | yes | reference/operations/dashboard.md (NEW) | per-call metadata; per-repo cost in result.md — done |
| p0176b | yes | reference/operations/dashboard.md (NEW) | /system LLM-cost card shows real per-call cost — done |
| p0176c | yes | reference/operations/dashboard.md (NEW) | failure summary, terminal sandbox states — done |
| p0177 | yes | reference/concepts/multi-agent-orchestration.md | sub-agents: master spawns children sharing sandbox/cost, `spawn_agents` tool, run-wide caps — done |
| p0179a | yes | how-it-works/skills-catalog.md | master-loop prompts are role:master SKILL.md files in the catalog — covered |
| p0179b | yes | how-it-works/methodology.md | coding presets collapse to one AgenticMaster step (plan+execute+verify in one loop) — done (also first-run trace) |
| p0179d | yes | reference/pipelines/security-scan.md | scan/legal presets collapse to master + sub-agents — done |
| p0179e | yes | reference/pipelines/mad-discussion.md | mad-discussion runs perspective masters + synthesizer — covered |
| p0179f | yes | how-it-works/lifecycle.md | approval works without a plan key — superseded by p0276 (plan is back before approval) |
| p0179g | no | — | one-line path bug fix |
| p0179h | no | — | internal multi-repo path-prefix addressing fix |
| p0180 | yes | how-it-works/multi-repo.md | sandboxes deduped by (repo, toolchain image) — done |
| p0182 | yes | reference/operations/dashboard.md | ProjectMap analyzer cache in Redis, survives restart — done (cost note) |
| p0183 | yes | reference/operations/dashboard.md | run detail = single execution-tree view — done |
| p0184 | yes | reference/operations/dashboard.md | run card shows ticket title, fetch-ticket detail — done |
| p0185 | no | — | dependency pin fix |
| p0186 | yes | reference/operations/dashboard.md | live-tail one-liners per step, active agent in header — done |
| p0187 | yes | connect-your-stuff/ai-providers.md | Claude subscription OAuth tokens (`sk-ant-oat01-…`) supported — done |
| p0188 | yes | connect-your-stuff/ai-providers.md | per-(provider,model) rate limiter (`rate_limit`, RPM/TPM) — done |
| p0189 | yes | reference/operations/dashboard.md | context.yaml parse errors with line/col; sandbox stdout inline — done |
| p0190 | yes | reference/operations/dashboard.md | freshness bar polish — done (rolled into dashboard page) |
| p0191 | yes | reference/configuration/tools.md | `get_artifact_credentials` tool for private feeds — gap noted (page exists, tool list not re-verified this phase) |
| p0192 | yes | how-it-works/lifecycle.md | commit refuses staged secrets — done |
| p0193 | no | — | internal write-path hardening |
| p0194 | no | — | deterministic pipeline-shape smoke tests |
| p0195 | no | — | preset contract test coverage |
| p0196 | no | — | mocked pipeline E2E harness |
| p0197 | no | — | sandbox-execution integration tests |
| p0198 | yes | how-it-works/lifecycle.md | SetupRegistryAuth pre-stages private-registry credentials — done (step named in first-run/lifecycle) |
| p0199, p0199b-f | no | — | real-composition pipeline harness tiers, internal test infrastructure |
| p0200 | yes | reference/operations/dashboard.md | UI cancel + run watchdog (max wall time) — done |
| p0201 | yes | reference/operations/dashboard.md | sandbox liveness + orphan reaper — done (operations notes) |
| p0202 | yes | how-it-works/lifecycle.md | language-agnostic InstallDependencies; benign zero-test not a failure — covered by p0216/p0241 text |
| p0202a | yes | reference/configuration/agentsmith-yml.md | `ci.install_command` context.yaml override — covered (context.yaml keys) |
| p0202c | no | — | internal failure-recovery predicate fix |
| p0202d | yes | reference/pipelines/index.md | re-running init-project preserves operator context.yaml edits — done (init rerun section) |
| p0202e | yes | reference/configuration/agentsmith-yml.md | analyzer-derived `prerequisites` command — covered |
| p0203 | yes | reference/operations/dashboard.md | per-step outcomes, per-step LLM cost + cache-hit — done |
| p0204 | no | — | internal preset-shape fix |
| p0205 | yes | reference/operations/dashboard.md | run detail two-pane master/detail — done |
| p0208 | yes | reference/operations/dashboard.md | dense runs list with filter chips — done |
| p0209a | yes | reference/operations/dashboard.md | app rail navigation — done |
| p0209b | yes | reference/operations/dashboard.md | /system master/detail — done |
| p0209c | yes | reference/operations/dashboard.md | cost + activity rollups — done |
| p0210 | yes | reference/operations/dashboard.md | load-catalog step lists skill/master/concept names — done |
| p0211 | yes | reference/operations/dashboard.md | run snapshot title/repos completeness — done |
| p0212 | yes | how-it-works/multi-repo.md | commands run in the module's actual directory — done |
| p0215 | yes | reference/configuration/agentsmith-yml.md | bare C# sandbox defaults to .NET 9 SDK — covered by p0245/p0265 image docs |
| p0216 | yes | how-it-works/methodology.md | rigid test gate dropped; master runs the repo's own tests — done |
| p0217-p0221 | yes | reference/operations/dashboard.md | dashboard design-system/typography/layout/catalog browser — done (rolled up) |
| p0222 | yes | reference/operations/dashboard.md | LLM activity transparency (intent + tool + narration) — done |
| p0223 | yes | reference/operations/dashboard.md | honest per-repo commit outcomes — done |
| p0224 | yes | reference/operations/dashboard.md | model label + analyzer honors declared repo type — done |
| p0225 | yes | reference/operations/dashboard.md | runs-list correctness fixes — done |
| p0226 | yes | how-it-works/multi-repo.md | persist skips untouched repos — done |
| p0227-p0229 | yes | reference/operations/dashboard.md | live run watching, command timeline, timeline layout — done |
| p0230 | yes | reference/configuration/agentsmith-yml.md | configurable sandbox command timeouts (global + per-project) — done |
| p0231 | yes | reference/operations/dashboard.md | unified chronological timeline; dashboard k8s manifests — done (+ host-it/kubernetes) |
| p0232 | yes | reference/operations/dashboard.md | cancel reason surfaced — done |
| p0233 | yes | reference/operations/dashboard.md | runs list live update — done |
| p0234 | yes | host-it/docker-compose.md | per-repo run record; dashboard image published to Docker Hub — done |
| p0235 | yes | connect-your-stuff/ai-providers.md | LLM network timeout default raised (300s), result view markdown — done |
| p0236 | yes | reference/operations/dashboard.md | self-explanatory LLM-timeout failure message — done |
| p0237 | yes | how-it-works/lifecycle.md | a run always finalizes: result.md leads with WHY — done |
| p0238 | yes | how-it-works/lifecycle.md | one-run-per-ticket by construction — done |
| p0239, p0239b, p0239c | no | — | deterministic verification harness + adapter tests |
| p0240 | yes | how-it-works/lifecycle.md | ProjectMap cache invalidates on HEAD SHA — done (analysis freshness note) |
| p0241 | yes | how-it-works/methodology.md | success is a code guarantee (code changed + verified green) — done |
| p0242 | yes | reference/operations/server-resilience.md | orchestrator-authoritative run liveness — covered |
| p0243 | yes | reference/operations/dashboard.md | analyzer output surfaced in run detail — done |
| p0244 | no | — | write rooting fix (agent edits land in /work) |
| p0245 | yes | connect-your-stuff/repos-mono.md + agentsmith-yml.md | per-language toolchain image config (`sandbox.images`) — done |
| p0246 | yes | host-it/docker-compose.md + kubernetes.md | relational system-of-record (DB required in server mode) — done |
| p0246a-c | no | — | persistence foundation / claim cutover / projector, internal slices |
| p0246d | yes | how-it-works/lifecycle.md | DB is system-of-record; tracker label is best-effort projection — done |
| p0246e-f | no | — | thin-notify transport + hub reshape, internal |
| p0246g | yes | host-it/kubernetes.md | `agentsmith database migrate` init-container, never on startup — done |

## p0247 — p0333c (+ p0324-p0329)

| Phase | User-visible? | Target doc page | Status / justification |
|---|---|---|---|
| p0247 | yes | reference/operations/dashboard.md | analyze.md shown in the Analyze step detail — done |
| p0249 | no | — | internal sandbox key→repo map fix |
| p0250 | no | — | internal sandbox addressing unification |
| p0251 | no | — | internal DB-lease claim cutover |
| p0252 | no | — | internal Redis heartbeat retirement |
| p0253 | no | — | internal result.md verdict fidelity fix |
| p0254 | no | — | internal SignalR fix + test de-flake |
| p0255 | no | — | internal master-executes-plan bugfix |
| p0259 | yes | reference/operations/dashboard.md | cancelled runs get a distinct terminal status — done |
| p0261 | yes | connect-your-stuff tracker pages | configurable `failed_status`; triggering rests on native status — done |
| p0262 | yes | reference/concepts/ticket-lifecycle.md + trigger-it/labels.md | lifecycle derived from native status + lease; labels output-only; re-run by reopening — done |
| p0263 | no | — | internal verdict re-prompt fix |
| p0264 | yes | reference/operations/dashboard.md | system stream polish — done |
| p0265 | yes | connect-your-stuff/repos-mono.md | LLM names toolchain image in context.yaml `stack.image` — done |
| p0266 | yes | reference/operations/dashboard.md | config explorer view — done |
| p0267 | yes | reference/pipelines/api-scan.md | api-scan delivers master triage findings — done |
| p0268 | yes | reference/operations/capacity.md (NEW) | per-stack sandbox sizing from context.yaml — done |
| p0269a | yes | reference/operations/capacity.md (NEW) | capacity-aware admission; Docker `max_concurrent_sandboxes` — done |
| p0269b | yes | reference/operations/dashboard.md | queued-run rendering — done |
| p0270a | no | — | internal materialized ResolvedConfig |
| p0270b | yes | reference/operations/dashboard.md | config explorer explains effective value + provenance — done |
| p0271 | yes | reference/operations/dashboard.md | per-project config detail sheet — done |
| p0272 | yes | connect-your-stuff/repos-multi.md + agentsmith-yml | sandbox secret injection (k8s) — covered by example config; noted on capacity page |
| p0273 | yes | how-it-works/methodology.md | keystone gates on regressions, not any red — done |
| p0274 | yes | reference/concepts/cost-tracking.md | live per-call cost uses `pricing` config — done |
| p0275 | yes | reference/operations/dashboard.md | run detail = stable step skeleton — done |
| p0276 | yes | how-it-works/methodology.md + lifecycle.md | plan generated + approved BEFORE the master executes — done |
| p0277 | yes | reference/pipelines/security-scan.md | delivery = master triage + uncovered High+ facts — done |
| p0278 | yes | reference/pipelines/security-scan.md | scan masters get read-only review surface + inline scanner output — done |
| p0279 | yes | reference/pipelines/security-scan.md | `agent.scan_min_source_reads` (default 6); unread source claims downgraded — done |
| p0280 | no | — | internal sub-agent subsystem completion |
| p0281 | no | — | umbrella parent, sliced a-e |
| p0281a | yes | connect-your-stuff/repos-multi.md | `connections:` catalog + repo discovery + glob refs — done |
| p0281b | yes | connect-your-stuff tracker pages | tracker owns the workflow; project declares only `resolution:` — done |
| p0281c | yes | get-it-running/install.md + host-it pages | single `deployment: {registry, version}` pin — done |
| p0281d | yes | trigger-it/cli.md | `--agent` runs project-less CLI scans — done |
| p0283b | no | — | internal composed discovery query |
| p0285 | yes | connect-your-stuff/repos-multi.md | exact `connection/<name>` refs resolve statically/offline; per-repo `default_branch` — done |
| p0292 | yes | reference/operations/dashboard.md | System → Connections diagnostics (probe repos/trackers, webhook panel) — done |
| p0293 | yes | reference/operations/dashboard.md | probes extended to LLM/sandbox/Redis/DB/chat — done |
| p0294 | no | — | internal tolerant JSON parsing for init |
| p0296 | yes | connect-your-stuff/tracker-gitlab-issues.md | GitLab API base derived from repo URL; `GITLAB_URL` optional override — done |
| p0297 | no | — | GitLab subgroup double-encode fix |
| p0298 | no | — | idempotent CreatePullRequest (re-run reuses open PR) |
| p0299 | no | — | multi-sandbox commit consolidation (correctness fix) |
| p0300a | yes | connect-your-stuff tracker pages | `lifecycle_status_names` opts into native workflow transitions — done |
| p0300b | no | — | no-silent-degradation hardening |
| p0300c | yes | trigger-it/polling.md + security-scan.md | discovery fetches only agent-smith-tagged tickets; verification-red PRs open as draft — done |
| p0315, p0315a-f | yes | how-it-works/spec-dialogue.md (NEW) | conversational design partner in Slack/Teams: discuss → answer / bug / phase / epic; /create-phase, /execute-phase — done |
| p0316 | yes | how-it-works/spec-dialogue.md (NEW) | ticket instruction contract; `ignored_instructions` in result.md — done |
| p0317 | yes | how-it-works/spec-dialogue.md (NEW) | ticket conversation + image/document attachments; `supports_vision` — done |
| p0318 | yes | how-it-works/spec-dialogue.md (NEW) + first-run | clarification gate; `needs_clarification_status` — done |
| p0320a | yes | reference/operations/capacity.md (NEW) | pipeline-aware sandbox sizing, LLM values clamped — done |
| p0320b | yes | reference/operations/capacity.md (NEW) | admission computes whole-run footprint — done |
| p0320c | yes | reference/operations/capacity.md (NEW) | persistent FIFO capacity queue, one entry per ticket — done |
| p0320d | yes | reference/operations/dashboard.md | queued runs first-class (amber, position, filter chip) — done |
| p0321 | yes | reference/pipelines/index.md (init) + lifecycle | init-project terminalizes without a PR; re-init via trigger status — done |
| p0322a | yes | reference/operations/dashboard.md | real x/y run progress; init runs carry ticket title — done |
| p0322b | yes | reference/operations/dashboard.md | speaking sandbox names `repo-<contextName>` — done |
| p0322c | no | — | internal ghost-PR gate + honest commit errors |
| p0323 | yes | reference/concepts/cost-tracking.md | prompt caching revived; cached share per LLM call; 0% = alarm — done |
| p0324 | yes | get-it-running/install.md + first-run.md + host-it | `agent-smith doctor` preflight; server startup preflight on /health — done |
| p0325 | yes | get-it-running/install.md + how-it-works/skills-catalog.md | skills embedded in the release; pin override-only — done |
| p0326 | yes | get-it-running/first-run.md | `agent-smith demo` golden-path demo — done |
| p0327 | yes | how-it-works/expectations.md (NEW) | durable dialogue: checkpoint at the ask, resume on the answer; `waiting_for_input` — done |
| p0328 | yes | how-it-works/expectations.md (NEW) | expectation negotiation: ratified Soll block = acceptance contract — done |
| p0329 | no | — | replay-golden eval harness + metric definitions (operator-side eval tooling, mentioned on expectations page) |
| p0330 | yes | reference/operations/capacity.md (NEW) | cancel is persistent state, enforced with force-kill — done |
| p0331 | yes | how-it-works/lifecycle.md + capacity.md | ScopeRepos narrows the run before provisioning; `ensure_repo_sandbox` — done |
| p0332 | yes | host-it/kubernetes.md + capacity.md | requests-based quota; resource-time next to LLM cost — done (kubernetes section pre-existing) |
| p0333 | yes | reference/pipelines/security-scan.md | master read-set merge (implicit rejection of read High+ facts) — done |
| p0333b | yes | reference/pipelines/security-scan.md | history-only secrets lifted to Critical — done |
| p0333c | yes | reference/pipelines/security-scan.md | generated files skipped for non-secret patterns — done |

## Totals

Counting each phase (rolled-up rows like `p0217-p0221` expanded per phase):

- Phases in range: 197
- User-visible: 145
- Internal-only: 52

The three spot doc-touches before this sweep (p0261, p0285, p0332) are counted as
user-visible above; their pages were extended rather than created.
