# Hard-coded limits inventory (2026-11-17-274c)

Rule 4 (principles.md): a limit has a reason the operator would accept, lives in config, and its
message names the value, the reason and the config key. The limits that predate the rule are
listed debt. This note is that list, at file and symbol level. The per-file counts live in
`tests/AgentSmith.Tests/Architecture/hard-coded-limits-baseline.tsv`, and
`HardCodedLimitRatchetTests` keeps new ones out: no new file, no growing count, and a cleared
file leaves. Paths are relative to `src/backend/`.

Every item has one of four classes:

- **AGENT GUARD** caps or cuts what an agent does, produces or sees.
- **RESOURCE** protects the process or infrastructure.
- **POLL** is a sweep, heartbeat or staleness interval.
- **DISPLAY** cuts text for a log, a page or a notification only.

Every item also has one of four dispositions:

- **removed**: the limit is gone.
- **config**: the limit lives in config and its message names the key.
- **debt**: the limit is kept for the class reason below and waits to be moved to config.
- **ask**: the limit is tied to an open operator question, so it waits for that answer.

## Done in this series

| Item | Disposition |
|---|---|
| `limits:` block (`LoopLimitsConfig`) | Was never bound into DI. It now binds in the CLI and the server, and the server follows its config store (274ca). |
| `limits.max_seconds_per_skill_call` | config. A scout call past it throws a TimeoutException that names value, reason and key (274ca). |
| `limits.max_sub_agents_per_run`, `max_sub_agents_per_dialog_turn` | config. A deferred spawn entry names value, key and scope (274ca). |
| `limits.max_tool_calls_per_skill`, `max_concurrent_sub_agents` | config. They bind now. The tool-loop stop still happens inside FunctionInvokingChatClient and has no message of its own (debt). |
| `max_tool_calls_per_investigator`, `max_tool_calls_per_verifier`, `max_llm_calls_per_skill`, `max_input_tokens_per_skill_call`, `max_concurrent_skill_calls`, `max_skills_per_phase` | removed and retired. Only the skill-call runtime read them, and nothing had used that runtime since p0312 (274cb). |
| `SkillCallRuntime` with `LimitEnforcer`, `PipelineConcurrencyGate` and `CallCostRecord.HitLimit` | removed (274cb) |
| `FileToolHandler` 100 KB read cap, with `FileReadTracker` | removed. It was never constructed (274cb). |
| `AgentDefaults` DefaultMaxTokens, CompactionMaxTokens, MaxFileStructureLines, MaxTextContentLength | removed. Nothing read them (274cb). |
| skill.yaml `discussion` (max_rounds 3, max_total_commands 50) | removed. Nothing read it (274cb). |
| `SandboxRedisChannel.EventBlockTick` | removed. Nothing read it (274cb). |

## AGENT GUARD: one by one

Default disposition is **debt**. The class reason: a cap or cut on what a model sees or emits
keeps one call inside its context window, its time and its money. Each cap that refuses the
agent should either become harness or move to config with a message that names the key.

### Look allowances and loop ceilings

These are **ask** (open question: the spec review loop and attack budgets).

- `Application/Services/Specs/DerivationLookTerms`: DerivationAllowance 12, CutReviewAllowance 6, SpecReviewAllowance 8, AttackReview 24 (+12 over a spec's cited paths).
- `Specs/Review/SeriesContractRound`: +12 over the cleared entries.
- `Specs/SpecDerivationCall`, `SpecCutReviewer`, `SpecPremiseChecker`, `ContradictionChecker`, `SpecDiffReviewer`: each MaxIterations, set to the allowance plus 2.
- `Specs/RatifiedMockReview.ExtraIterations` 3.
- `Specs/AccountSearchBudget.PerPass` 12, with `AccountTools.MaxIterations`. The refusal names the value and no key.
- `Specs/Review/SpecRegroundSession.MaxPairs` 24.
- `Specs/SpecSetDeriver.MaxAttempts` 3.

These are **debt**:

- `ProjectAnalyzer.ExplorationBudget` 25, attempts 2.
- `Handlers/MasterReengagementLoop.HardSafetyCap` 50. It is the anti-hang net used only when no cost cap is set.
- `Tools/ContextWriteRejectionBudget.Limit` 5.
- `Infrastructure/Services/Factories/ChatClientFactory.MaxIterationsPerRequest` 25. This is the default tool loop.
- `Factories/ToolLoopErrorTolerance.ConsecutiveErrorsPerRequest` 3.
- `Factories/ReplayChatClientFactory` 25 iterations, 4096 output tokens.
- `Contracts/Pipeline/StepBudget`: Default 100, BaseAllowance 40, PerSpecAllowance 20, AbsoluteCeiling 400.

### Tool-argument refusals

These are **ask**:

- `Tools/DefaultDecisionToolHost`, under the open question about the product's decide-by-default feature: question 500, chosen 500, why 1000, alternatives 8 × 300.
- `Tools/HandForwardToolHost`, under the open question about the build-note cap: 20 entries × 300 chars.

These are **debt**:

- `Tools/MemoryDisputeToolHost.MaxReasonChars` 500.
- `Tools/ComparePairs.MaxPairs` 30.
- `Browser/BrowserStyleProperties`: MaxSelectors 20, MaxSelectorLength 200.
- `Tools/DesignReadToolHost`: DefaultDepth 3, MaxDepth 6.
- `Specs/QuestionHandbackShape.MinReadings` 2. This is a shape rule, i.e. harness.
- `Contracts/Progress/ProgressLedger`: MaxItems 40, MaxNoteLength 500.
- `Contracts/Models/ObservationCaps`: description 500, suggestion 300, rationale 500, details 4000.
- `Contracts/Memory/ProjectMemoryRecord` (ProjectMemoryLimits): description 200, body 4000, indexed 191. The indexed value is an index width.
- `Contracts/Tickets/TicketTitle.MaxLength` 255. This is the tracker's own limit.
- `Infrastructure/Services/ToolImages/ToolImageLoopFrame`: MaxPerCall 2, MaxPerLoop 4.
- `ToolImageRule.MaxLongEdgePixels` 1568. This is the provider's image bound.
- `SkillsPackaging/SkillDescriptionRule.MaxChars` 200. This is the skill loader's own bound.

### Tool results the agent sees

All of these are **debt**. Every cut says so in its result. A config key belongs in the message
once these move to config.

- `Contracts/Services/ToolResultBound.DefaultBudget` 100,000 chars per tool result.
- `Sandbox.Wire/SizeLimits`:
  - read_file 1 MiB, write_file 10 MiB.
  - list_files 1000, grep head 1000, directory_tree 1000.
  - exploring result 40,000.
  - run_command buffer 1 MiB, stdout 60,000, stderr 20,000, error line 8,000.
  - captured stdout 1,000,000.
  - The in-process sandbox's directory_tree has no entry cap, so its parity with the container is open.
- `Sandbox.Wire/GrepScope.MaxFileSizeBytes` 1,000,000. `Infrastructure/Services/Sandbox/DirectoryTextSearch` uses the same value.
- `Sandbox.Agent/Services`: GrepStepHandler match line 240, StepExecutor 1,000,000, ProcessRunner failure tail 8 KiB. Default depths: list_files 1, directory_tree 4.
- `Application/Services/Tools`: WebToolHost 100,000; StreamCapture and BoundedRunCommandOutput 60,000 and 20,000.
- `Tools/ContextDefectReport`: 4000 / 6 / 8 / 300 / 220.
- `Loop/SubAgentAnswer`: Ceiling 60,000, CallCeiling 200,000, AimWords 1,500.
- `Tools/PriorRunLedgerSeeder.MaxAge` 72 h.
- `Design/FigmaNodeLine` 200. `Design/FigmaExportScale`: 1568 px, scale 0.01–2. `DesignReadToolHost`: NodeBudget 16,000, VariableBudget 6,000.
- `Browser`: VisualComparisonLines 60, RenderResultText 50 / 240, VisualComparisonRecorder 4 images per run.
- `Specs`:
  - SearchOutcome 4,000.
  - RepositoryFileReadTool 16,000 per page.
  - SourceScopeLook head 200.
  - DependencyAuditTool 12,000.
  - SpecRunCommandBudget 30,000 / 400 / 200 / 80.
  - CommandElision 65 %.
- `Infrastructure/Services/Providers/Agent`:
  - ContextPressureFinalizingChatClient HardBoundRatio 0.85.
  - ForwardedViewFit FloorChars 4,000.
  - CompactionSummaryRequest 200,000 / 2,000.
  - LargestToolResult 0.5.
  - ChatClientFactory compaction summary 1024 output tokens.
- `Infrastructure/Services/Zap/ZapInstanceExchange.MaxBodyChars` 2000. `Security/PatternFileMatcher`: match 200, 5 header lines.

### Prompt sections

All of these are **debt**, except where marked **ask**.

- `PrDiffPromptRenderer.MaxRenderedLines` 4000.
- `SwaggerSpecCompressor`: 100,000 / 240.
- `Handlers/SchemaFlattener`: enum values 20 / 15.
- `SecurityFindingsFormatter`: 15 / 80.
- `Handlers/ZapTopSelector` 30, `SpectralTopSelector` 3 / 30, `NucleiTopSelector` 20.
- `Specs`:
  - SpecDiffText 120,000.
  - DiffWindows 400,000.
  - SpecReviewPrompt 40,000 / 20,000.
  - SpecCutReviewPrompt 200,000.
  - Review/SeriesContractPrompt 60,000 (**ask**: series contract).
  - SpecBuildNoteSection 8,000 (**ask**: build-note cap).
- `SpecExecution/SpecFormatFailureRecorder` 800. `SpecExecutionPromptBlocks` 6 staged lines.
- `Handlers/WorkingStateSection`: 30 paths, 12 decisions, 6 staged lines.
- `Handlers/AcceptanceClaimNudge` 30. `GateRetryCoordinator` 2,000. `ObservationRecoveryHelper` 2,000.
- `Handlers/RunDirectoryReader.WikiBudgetChars` 60,000.
- `Handlers/PrReviewFindingSelector.MaxInlineComments` 25.
- `Prompts`:
  - TicketConversationPromptSection 20,000.
  - EpicGroundPromptSection 20,000.
  - TicketImagePromptParts 10 images.
  - DialogImageParts 4 images.
  - DesignSystemPromptSection 32,000.
  - DesignReferenceSection 10.
- `Sandbox/ToolchainCapabilityLine` 12. `Scans/CitedCodeWindow` 12 lines.
- `Validation/SchemaValidator.MaxClauses` 10.
- `Memory/RunNarrativeComposer`: 5 / 3 / 80 / 160.
- `Scope/RepoScopeEvaluator.ConfidenceFloor` 0.7.
- Citation matching heuristics:
  - `Specs/Review/SpecAttackPrompt` quote words 4.
  - `SpecQuotableText` 2.
  - `SeriesContractAdmission` 12.
  - `SpecPremisesReader` 4.
  - `CitationMatch` 60 %.
  - `SpecCitationLocator` 20 / 10 / 2.

## RESOURCE: debt, by group

Class reason: each one protects a process, a host or an external API from one slow or oversized
answer. The value either follows the external system's own bound or is a timeout for one
sandbox step.

- **Sandbox step timeouts** in Application:
  - git steps are 120 s. `StructureGitRunner` is 60 s.
  - checkout: clone 300 s, checkout 60 s, fetch 300 s, local 60 s.
  - verify command 1800 s. Install 900 s.
  - probes: 45, 60 and 120 s.
  - browser render 240 s, browser readiness 600 s.
  - file steps 30 s, markitdown 120 s, merge 300 s.
  - preflight checks 60 s each.
  - run_command default 60 s. http_request 15 s, clamped to 1–60 s.
  - web 30 s.
  - ask-human 5 min, approval 3 days, hot wait 600 s, inline asks 10 min.
- **Network and providers** in Infrastructure:
  - LLM HTTP 10 min. OpenAI fallback 300 s.
  - OAuth retries 5, with backoff 5–60 s.
  - RetryWait ceiling 120 s, floor 15 s. The rate-budget defaults apply per provider.
  - Figma 30 s and Retry-After 30 s. Swagger 30 s.
  - draft checks 10 s and 30 s.
  - tracker and source pagers: page size 100 or 200, 10–50 pages. Each warns when cut.
  - provider caches: 30 min. Trust verdicts: 5 min.
  - worker retries 3. `GuardedRedirectHandler` 5 redirects. Registry transport 5 s and 4 MiB, tags 1000 × 5 pages.
- **Streams and buffers**:
  - event stream 10,000 entries, 24 h TTL, cursor 48 h.
  - dialogue stream 1000 entries, 2 h TTL.
  - sandbox event channel 1000, drain 10 s, retries 200 ms / 1 s / 5 s.
  - output batcher 50 / 100 ms.
  - prompt recorder queue 1024, recent records 64 / 256.
  - RunDbProjector 25 entries.
  - archive batches 500 / 64 MB.
  - system ring 10,000.
  - read pages of 100.
- **Uploads** in Server: archive 8 GiB. Reference sets 500 files / 25 MiB / 25 MiB per file, 100 MiB per conversation, 26 MiB per route, path 240. Images 5 MiB. Server memory floor 512 MiB.
- **Scans**:
  - git history 500 commits. Source file 1 MiB. Listing depth 12.
  - regex timeouts: 0.2–2 s.
  - audit process 60 s. Spectral 120 s.
- **Locks and leases**:
  - claim lock 30 s, launch locks 30 s.
  - label lock 10 s, decision file lock 30 s.
  - cancel kill grace 30 s, shutdown grace 10 s, channel grace 30 s.
  - Docker sandbox default bound 2.
- **Server reads and pages**:
  - delivery reader 5000, offset 1000.
  - PR list 200.
  - change sweeps: 10 pages, 10 PRs per cycle, 50 / 50 re-reads.
  - rework nudges: batch 10, backoff 15 s → 900 s.
  - dependents wake: 24 h, 50 (**ask**: dependents wake).
- **Persistence**:
  - SQLite busy 5 s.
  - lease attach 2.
  - rework keys 200.
  - run retention 90 days.
  - prior attempts scanned 20.
  - Redis project map 30 days.
  - job keys 24 h.

## POLL: debt, by group

Class reason: an interval is how often a loop looks. A staleness threshold is how long silence
may last before the loop acts. Both depend on the deployment more than on the agent.

- **Run liveness**:
  - heartbeat 45 s, watchdog 1 min.
  - lease fresh 3 min. Reaper scans every 1 min.
  - liveness reaper 1 min.
  - queued sweep 30 s, lost after 60 s. Reconcile every 10 min.
- **Leadership and reload**:
  - leader TTL 30 s, renew 10 s, idle backoff 5 s.
  - epoch, config and skills watches 3 s.
- **Sandbox**:
  - pod and heartbeat polls 2 s, miss threshold 3.
  - step watch 3 s, 2 misses.
  - orphan scan 30 s. Minimum age 60 s.
  - agent heartbeat 2 s, key TTL 10 s, idle 60 s × 30.
- **Buses and streams**: Redis bus 500 ms, dialogue 500 ms, job queue 1 s, broadcaster 200 ms. DB discovery 30 s. Trail flush 500 ms.
- **Sweeps**:
  - PR re-read 5 min, staleness 15 min. Recent PR records 24 h.
  - change sweep floor 10 s, budget half the interval.
  - cursor margin 5 s. Tracker look-back overlaps of 60 s.
- **Other**:
  - capacity pump 15 s.
  - chat follower 30 min, binding watcher 5 s.
  - clarification 2 h.
  - master question patience 14 days.
  - retention cadence 24 h, caller observations 30 s and 1 h.
  - authority probe 30 s, repo discovery 5 min.
  - build-mismatch window 10 min.
  - Preconditions minimum recheck interval 60 s. This is a refusal of the config value and already names its key.

## DISPLAY: debt

Class reason: these cut text for a person or a log. Nothing the agent decides depends on them.

- **Log tails and previews**: 120–4000 chars in ErrorFormatter, the PR outcome text, the observation parser, the gate and verify output, the install output and the HTTP error bodies.
- **Run result texts**: 60 / 40 / 40.
- **Snapshot and summary lists**: `.Take(5)`, `.Take(3)`, 8 tag suggestions.
- **Event and trace summaries**: 200 / 120 / 80. The live output tail is 40 lines / 4000 chars.
- **Slack**: section 3000 and option 75. Both are Slack's own limits. Chat ticket list 20.
- **Output strategies**: the finding first line 80. The AzDO PR description 4000 is AzDO's own limit.
- **Secret masking**: minimum length 6, plus the mask widths.
- **Charts**: 2000 points, cold-call threshold 1024 tokens.
- **System page**: backlog 50, last-24-hours window, container log tail 200.
- **References**: preview 200 KiB / 8 KiB sniff, largest 3, left-out 20.

## Areas other work is rebuilding

These are not changed here and are listed for whoever rebuilds the area.

- **Filing, spec dialog, proposal and spec storage**:
  - `Server/Services/SpecDialog/`:
    - TicketRunText 2000
    - TicketRunsReviews 600 / 40
    - BoundTicketReader 8000
    - SpecDialogDigestWriter 4000 output tokens
    - SpecDialogWorkPictureStore and `Contracts/Models/WorkPictureLimits` 2000
    - FiledWorkRunsReader 10
    - TicketSearchAcrossTrackers 3 / 20
    - TicketGraphLinks 60 s
    - SpecDialogConversationList 50 / 200
    - SpecDialogConversationTitle 120
    - RunningDialogTurn 50
    - OutcomeShapes 40
    - DialogActivityRecorder 500
    - SpecDialogImageBody 5 MiB
  - `Application/Services/SpecDialog/RepositoryLayoutReader`: 40 / 20 / 4 / 10 min.
  - `Contracts`:
    - IConversationMocks 90,000 / 20 / 100 (**ask**: mock caps)
    - ReferenceCoverage 25
    - SeededTicketLimits 20,000
    - ConversationSubjectLimits 120
    - SeriesConfig.MemberLetters 26
  - `Application/Services/Specs/SpecIdFactory` 50.
  - `SpecExecution/SpecRecordIndexLine` 400.
  - `Polling/ApprovedTicketAdmission` 50.
  - `Persistence/Services/SpecRename/RowPages` 200.
- **Init, bootstrap and branch cleanup**:
  - BootstrapArtefactWriter, BootstrapPrinciplesTransfer, BootstrapContextRetirement, BootstrapMetaFiles: 30 s each.
  - BootstrapDocumentHandler: 2000, 120 s.
  - BootstrapDiscoverHandler 2000.
  - `Server/Services/Init/InitRunLauncher` 30 s.
  - `Infrastructure/.../LocalBranchFastForward` 30 s.
- **Ticket, run and home page reads**:
  - TicketListQuery 5000.
  - TicketListRequest 50 / 200.
  - RunStepQueryEndpoints 20 / 100.
  - RunListComposer 50 / 200.
  - TrailReader 500 / 2000 / 100 / 200.
  - DialogActivityReader 200 / 500.
  - `Tickets/Checks`: TicketCheckKicks 100 / 10 / 5 min, TicketBetweenAttemptsChecker 5 min, TicketStuckRule 24 h.
  - TicketThreadReader and TicketTrackerTextReader 60 s.
  - TicketPullRequestReader 5 min.
  - ConversationAssembly 10 min / 1 s.
- **The in-process sandbox's local clone**:
  - `Infrastructure/Services/Sandbox/InProcessSandbox`: stdout budget 1,000,000, failure tail 8 KiB.
  - `Providers/Source/HostSourceCloner` 300 s.
  - `LocalGitCommand` 60 s.
  - `Application/Services/Sandbox/WorkCloneOpener`: depth 50, 300 s, 60 s.

## Open dead-code findings, not limits

- `EventPublishingAIFunction` and `BoundedResultAIFunction` were built only by the removed skill-call runtime. No live run publishes ToolCall or ToolResult events today, although the dashboard renders them. The per-result bound reaches tool loops through ToolImageFunctionInvokingChatClient instead.
- The skill-output validator cluster (`SkillOutputValidatorFactory` and the plan, diff, bootstrap, observation and discovery validators) is registered and has no runtime caller.
- `SkillConfig` / `LoadProjectSkills` has no caller.
- `CallCostRecord.ToolCallCount` is always 0.
