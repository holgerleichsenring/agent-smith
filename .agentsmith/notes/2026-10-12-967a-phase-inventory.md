# "phase" inventory — 2026-10-12 (series 2026-10-12-967a)

Snapshot of every remaining "phase" after the 03c7 noun change (phase → spec), taken on
origin/main 34987b012 and agent-smith-skills origin/main 99622f6. Dated, like every note:
re-run the counts before a slice starts, the tree moves.

Three groups:

- **G1 — the old noun for spec.** Rename.
- **G2 — another meaning.** Keep: execution stage (`SkillExecutionPhase`), skill workflow
  steps ("Phase 1 — Inventory"), Kubernetes pod phase, triage grouping.
- **H — history.** Keep: `.agentsmith/specs`, `.agentsmith/decisions`, `.agentsmith/notes`,
  EF migrations, CHANGELOGs, `state.done` index lines, run logs.

## Scale

| Area | Files | Matches | G1 | G2 |
|---|---|---|---|---|
| src/backend + tests (no migrations) | 1,033 | 6,109 lines | ~87 % | ~620 lines, 115 files (+14 mixed) |
| src/dashboard | 83 | 706 | ~611 | 93 (+2 ambiguous) |
| skills repo (no CHANGELOG) | 10 | 109 lines | 65 | 44 |
| docs/ | — | 261 lines | ~242 | 16 (+3 history) |
| CLAUDE.md, README, WORKER-MODE, website | — | 58 lines | 55 | 3 |
| .claude/ (gate, tests, prompt.md) | — | 133 lines | all | — |
| .agentsmith schemas, contexts, memory | — | ~200 lines | descriptions, principles.md, memory bodies | schemas/ "Phase C of D4" |

## G2 — keep (the allowlist the guard starts from)

| Token | Where | Meaning |
|---|---|---|
| `SkillExecutionPhase` and its string form: `CallScope.Phase`, `LlmCall*/ToolCall*Event.Phase`, `WorkerRequest.Phase`, `CallCostRecord.Phase`, columns `RunEvents.Phase`, `RunLlmCalls.Phase`, `GuardErrorKind.WriteForbiddenInPhase` | Application/Models/SkillExecutionPhase.cs | execution stage of a model call |
| `PhaseCost`, `RunCostSummary.Phases`, `RepoCost.Phases`, result.md `cost.phases:` | cost tracking | cost per execution stage |
| `TokenUsageTracker` (`PhaseUsage`, `SetPhase`, `GetPhaseBreakdown`) | Infrastructure agent | scout / planning / primary / compaction |
| `CommandNames.RunReviewPhase`, `RunFinalPhase`, `RunVerifyPhase`, `RunVerifyPhaseContext(Builder)` | Contracts/Commands | triage / verify stage |
| `LoopLimitsConfig.MaxSkillsPerPhase`, config key `max_skills_per_phase`, dashboard "Max skills per phase" | config | skills per triage stage; persisted key |
| `KubernetesPodWatcher` `pod.Status.Phase` | Server/Services/Sandbox | Kubernetes |
| `*PhaseDataFlow*`, `PhaseDataFlowEdge`, `ContextKeys.ActivePhaseStep` | PipelineDataFlows | "pipeline step" — a third meaning; renamed to `*StepDataFlow*` in 967ac |
| retired pipeline name `phase-execution` | RetiredPipelineNames.cs:26, docs, dashboard tests | alias the server still rewrites |
| dashboard `StoryPhase` (`running`/`needsYou`/`relaunching`) | jobs/story | UI display state |
| skills "## Phase N" headings: api-security, security, legal-analyst, mad-discussion; `references/phase-discipline.md` and `{{ref:phase-discipline}}` | skills repo | workflow steps |
| WORKER-MODE.md `"phase": "Implementation"` | worker envelope `agentsmith.worker/1` | execution stage; wire key |

Dead code found on the way (delete, not keep): `Contracts/Models/Skills/PipelinePhase.cs`,
`PhaseAssignment.cs`, `ContextKeys.CurrentPhase` (Skill.cs:20); `ContextKeys.PhaseAppliesTo` has
no producer outside tests.

## G1 — wire and persisted surfaces (a rename breaks stored data or another process)

| Surface | Defined | Breaks how |
|---|---|---|
| DB columns `RunDecisions.PhaseId`, `RunSteps.PhaseId` | Entities/RunDecision.cs:39, RunStep.cs:33 | needs a column-rename migration (precedent `RenameRunPhasesToRunSpecs`) |
| `RunEvents.Type` stores the EventType NAME (`PhaseStateChanged/Recorded/Reviewed`, 79-81) | EventType.cs:101-109; read by `Enum.TryParse` EventEnvelopeSerializer.cs:62 | old rows become unresolvable, replay skips them |
| `RunEvents.PayloadJson` PascalCase `PhaseId` on the three events + `StepStarted`, `DecisionLogged` | PhaseLifecycleEvents.cs, L1StepEvents.cs:35, L2OrchestratorEvents.cs:15 | old payloads deserialize with a null id |
| Artifact kinds `phase_record:`, `phase_review:` | RunSpecProjection.cs:21,25 | every stored record/review disappears from readers |
| `ApprovedSeries.ContentJson` camelCase `phases`, `phaseId`, `executedPhaseIds` | SpecApprovalJson.cs | stored approvals read with null set members; `Read` only checks `Set not null` (:37) |
| Dialog outcome JSON `{"kind":"phase","phase":{…}}`, findings `phaseId` | OutcomeProposalJson.cs:34,43 | unknown kind THROWS |
| Transcript `SpecDialogTurnKind.PhaseProposal` stored as `"phaseProposal"` | SpecDialogTurnKind.cs:13 | silently becomes an Answer turn |
| `SpecDialogFiling.Kind = "phase"` | SpecDialogProposalComposer.PhaseKind | dashboard compares against it |
| Acceptance JSON `Declined[].Phase` | DeclinedCriterionView.cs | attribution lost (tolerant) |
| Resume checkpoints: command names, `ContextKeys` values (`PhaseSpec`, `PhaseAccounts`, `PhaseCommands`, …), assembly-qualified type names incl. namespace `PhaseExecution` | PipelineContextSerializer.cs:93 | entries silently skipped as "unknown type" |
| `CommandNames` values `*PhaseCommand` (8 G1) | CommandNames.PhaseBlock.cs, CommandNames.Pipeline.cs | stored `RunSteps.CommandName` loses label/class; custom presets break — `RetiredCommands` (CommandNames.cs:19) is the existing mechanism |
| Config key `trackers.*.work_item_kinds.phase` | TicketFilingRole.cs:30, TicketKindResolver.cs:46 | unknown key warned and ignored → silent fallback to Task |
| Tracker label `phase` (+ `phase-spec:approved`, `phase-epic`, `phase-parent:<id>`) | PhaseTicketRenderer.cs:28 | labels on existing tickets stop routing; documented as "the word a person types" |
| Hub events + REST JSON bodies (`phaseId`, `phases`, `phase`) | Contracts/Events, RunStatisticsView, RunStepView, ApprovedSetView, SpecDialogPhaseView, SpecDialogProposalPush | dashboard mirror `hub-events.ts` (hand-maintained, checked by `gen:hub-events`) must change in the same commit |
| LLM wire: derivation reply key `"phases"`, template token `{MaxPhases}` | SpecDerivationParser.cs:52,75; SpecSetDeriver.cs:112 | skills repo spec-derivation-master :7, :23, :78 — lockstep |
| LLM wire: cut-review answer `"phase_id"` | SpecCutReviewPrompt.cs:50 → `CutFindingWire.PhaseId` (snake_case) | every finding id dropped if only one side changes |
| Prompt headings `## Phase {i} of {n}: {id}`, `### This phase's design mock`, `## Phase contract` | SpecPromptSection.cs:37, :68; PhaseExecutionPromptFactory.cs:62 | coding-agent-master quotes "the phase section above" — lockstep |
| Gate hook path | .claude/settings.json:9, PhaseSeriesIdTests.cs:41, test_commit_message.py:28 | an open session keeps calling the old path; a missing hook fails OPEN |
| Gate ledger `.claude/phase-gate.log`, env `PHASE_GATE_LOG/SELFTEST/CORES` | phase-gate.sh:53, :205-217, :430 | history orphaned unless moved; self-test isolation breaks if hook and tests disagree |

No YAML/JSON key in files written to customer repos contains "phase" (the key is `spec:`).
Exception: `PhaseIdFactory.cs:21` fallback slug `"phase"`.

## G1 — names that need a non-mechanical choice (a blind Phase→Spec collides)

| Old | Blind rename | Choose |
|---|---|---|
| `SpecPhase` | SpecSpec | `DerivedSpec` |
| `ContextKeys.PhaseSpec` | SpecSpec | `CurrentSpec` |
| `PhaseSpecGate*` | SpecSpecGate | `SpecGate*` |
| `PhaseSpecProse` | SpecSpecProse | `SpecProse` |
| `PhaseAccounting` | SpecAccounting (exists, Contracts/Specs) | `SpecDeliveryAccounting` |
| `PhaseCommandLog/Entry/Scope/Budget` | next to chat `SpecCommand` | `SpecRunCommand*` |
| `SpecPhaseMocks` | SpecSpecMocks | `SpecMocks` |
| `SpecDialogPhaseView`, dashboard `SpecDialogPhaseProposal/Fact` | SpecDialogSpecView | `SpecDialogDraftView`, `SpecDraftProposal`, `SpecDraftFact` |
| `PhaseSpecFile` (tests/Architecture) | SpecSpecFile | `SpecYamlFile` |
| `VerifyPhase` (spec block build+test) vs `RunVerifyPhase` (G2 stage) | — | `VerifySpec` — also ends an existing confusion |
| `PhaseDraft` | SpecDraft (beside `SpecDraftOutcome/Validator`) | `SpecDraft` — acceptable |

## G1 — the bulk (mechanical once the above is fixed)

- **Backend:** ~150 types, namespace `Services.PhaseExecution` (15 files), 99 file names in src,
  52 in tests, ~400 test method names, 25 structured log templates (`{PhaseId}`), ~1,740 comment
  hits of which 167 "this phase" = the development increment.
- **Prompts to an LLM:** 25 files, ~97 lines outside comments (SpecCutReviewPrompt 24,
  SpecPromptSection 19, PremiseCheckPrompt 17, PreviousCutPromptSection 16,
  PhaseExecutionPromptFactory 11, PhaseReviewPrompt 10, MasterPromptSections.cs:81).
- **Human-facing text:** SpecPrBody (14), SpecSetComment, SpecMarkdown `## Phases`,
  CommandDisplayNames.Labels (11), CommandProgressLabels (10), OutcomeShapes buttons.
- **Tests infrastructure:** baselines `phase-name-baseline.tsv`, `phase-record-baseline.tsv`,
  `phase-record-length-baseline.tsv`, env `AGENTSMITH_WRITE_PHASE_BASELINE`,
  `file-length-baseline.tsv` paths; harness `PhaseReviewScript`, `PhaseTicketProvider`;
  recorded fixtures `analyzer-null-file-count/*.prompt` still carry `phase: p0001a`.
- **Dashboard:** `PhaseAccount.tsx`, `NavRail` `PhaseHeader`, `DialogProposalPanel`, test ids
  `phase-*`, `data-phase`, `splitPhasePrefix`/`PHASE_PREFIX_RE` (mirrors
  `RunStepsReader.PhaseQualifiedRegex`), `PHASE_STATUS`, ~7 visible strings ("One phase",
  "File this phase?", "This phase ran no sandbox command." …). Concentrated in
  SpecDialogSurface.test.tsx (111), hub-events.ts (31), DialogProposalPanel.tsx (33).
- **Skills repo:** spec-derivation-master (42, all G1), coding-agent-master (17 G1 + 18 G2
  mixed on one page), design-partner-master:247, references/memory-discipline.md:33,75,
  references/writing-documents.md:13,20,28.
  The coding agent's prompt today holds TWO "## Phase 2" headings: the backend's
  `## Phase 2 of 3: <id>` and the skill's `## Phase 2 — Execute`.
  Rename the skill's step headings to names ("## Execute", "your verdict"), not "Step N" —
  "step" is already the ledger and plan-step word on that page and the `steps:` YAML key.
- **Docs:** whole pages `reference/architecture/phase-workflow.md`,
  `reference/concepts/phases-and-runs.md` (linked from mkdocs.yml:131, :170 and two index
  pages), built-by-the-method.md, self-documentation.md, assets/control-flow.svg (20),
  labels.md `#phase-tickets` anchor.
- **Repo instructions:** CLAUDE.md (41 lines, three section headings), README (7),
  principles.md:202-211, `.claude/prompt.md` (stale: `p{NN}-*.md`, `decisions.md` — rewrite or
  delete), `.claude/commands/smoke.md:5`, `tools/audit-phases.py` (19; unreferenced),
  schema descriptions in spec/decision/context.schema.json, website (7), deploy/k8s:131.
- **Memory:** bodies only — the three `feedback_*phase*` slugs are cited from history
  (decisions p0162, p0521, 2026-09-17-042ef; spec p0439), so the slugs stay.
  `feedback_one_decision_yaml_per_phase.md:13,32` still shows the old key `phase:`.
