# Decision Log

## p01: Core Infrastructure
- [Architecture] Command Pattern (MediatR-style) as central pattern — strict separation of "What" (context) from "How" (handler)
- [Architecture] Single generic handler interface `ICommandHandler<TContext>` instead of multiple handler types — enables DI-based dispatch
- [Tooling] YamlDotNet for configuration — chosen for simplicity, avoids overengineering (no JSON schema validation at this stage)
- [Implementation] Lazy environment variable resolution in config loader — unset vars aren't errors during loading, only at usage time
- [TradeOff] No validation of all environment variables upfront — accepted incomplete config to gain flexibility

## p02: Pipeline Handlers
- [Architecture] CommandExecutor uses `GetService<T>()` instead of `GetRequiredService<T>()` — allows custom error messages instead of DI exceptions
- [Implementation] Handlers are sealed classes with primary constructors — enforces immutability and modern .NET 8 conventions
- [TradeOff] LoadCodingPrinciplesHandler can be fully implemented in Phase 2 but intentionally stubbed — accepted inconsistency to keep phases parallel
- [Implementation] ApprovalHandler auto-approved as stub — establishes pattern for later headless mode

## p03: Providers
- [Architecture] Separate provider for each source control system (GitHub, Local, GitLab later) — each has different API, no shared abstraction
- [Tooling] ClaudeAgentProvider chosen as primary agent — Anthropic API, agentic loop pattern established as reference
- [Architecture] ToolExecutor separated from agent loop — allows code reuse across provider implementations
- [TradeOff] Scout phase (file discovery) designed but deferred to Phase 9 — accepted complexity later to unblock core provider work
- [Security] Path validation in tools (no `..`, no absolute paths) — prevents directory traversal attacks from agentic code execution

## p04: Pipeline Execution
- [Architecture] Regex-based intent parser instead of LLM call — chosen for determinism, cost, and Phase 4 speed
- [Architecture] CommandContextFactory maps command names to contexts — avoids massive switch statement in PipelineExecutor
- [Implementation] ProjectConfig passed as parameter to PipelineExecutor, not injected — enables per-run configuration variation
- [TradeOff] Intent parser intentionally simple — can be swapped for Claude-based parser later without interface changes

## p05: CLI & Docker
- [Tooling] System.CommandLine instead of manual argument parsing — provides --help, validation, version support "for free"
- [TradeOff] No sub-commands, no daemon modes, no interactive shell — "Simple: Input in → PR out → Exit"
- [Architecture] Multi-stage Docker build — separates build (SDK) from runtime (slim), targets <200MB image size
- [Implementation] Config mounted as volume, not baked into image — enables reuse without rebuild

## p06: Retry & Resilience
- [Tooling] Polly via `Microsoft.Extensions.Http.Resilience` chosen over custom retry logic — standard, battle-tested, reduces code
- [Architecture] Retry config as typed class, not raw numbers — enables configuration without code changes
- [TradeOff] Conservative defaults (5 retries, 2sec initial, 2.0x backoff) — balances safety against cost overruns from excessive retries
- [Implementation] Exponential backoff with jitter — prevents thundering herd on rate limit recovery

## p07: Prompt Caching
- [Architecture] `PromptCaching = AutomaticToolsAndSystem` for all API calls — caches system prompt, tool definitions, coding principles
- [Implementation] Coding Principles positioned first in system messages — maximizes cache prefix (longest stable content cached)
- [TradeOff] 5-minute ephemeral cache (API default) accepted — balance between cost savings and context freshness
- [Architecture] TokenUsageTracker separates cache metrics tracking — enables observability without coupling to loop logic

## p08: Context Compaction
- [Architecture] Haiku for summarization, not custom rule-based compression — LLM-generated summaries more useful than keyword extraction
- [Architecture] FileReadTracker deduplicates file reads — prevents same file content from inflating conversation history
- [TradeOff] Last N iterations always preserved fully — accepted overhead to maintain recent decision context
- [Implementation] Compaction triggered periodically, not continuously — prevents overhead from constant summarization

## p09: Model Registry & Scout
- [Architecture] TaskType enum maps task to appropriate model — Haiku for discovery, Sonnet for coding, optional Opus for reasoning
- [Architecture] Scout as implementation detail of ClaudeAgentProvider, not separate interface — keeps IAgentProvider stable
- [TradeOff] Scout is read-only (no write_file, no run_command) — accepts limitation to prevent Scout from breaking code
- [Architecture] Backward compatible: Models config is optional — existing single-model configs work unchanged

## p10: Container Production-Ready
- [Architecture] `--headless` flag auto-approves plans — enables container/CI usage without TTY or interactive prompts
- [Implementation] Non-root user in Docker — security best practice
- [Architecture] SSH keys mounted read-only from host — enables git operations without embedding credentials

## p11: Multi-Provider
- [Architecture] Each provider self-contained with own agentic loop — APIs too different (tool format, caching, streaming) to abstract
- [Architecture] IAgentProvider interface stable across all providers — GeneratePlanAsync, ExecutePlanAsync only methods exposed
- [Tooling] No shared "LLM abstraction layer" — accepted code duplication to gain provider-specific optimization freedom
- [TradeOff] Scout and caching Claude-only for now — other providers do direct execution, accepted feature parity gap

## p12: Cost Tracking
- [Architecture] Prices in config, not hardcoded — providers change pricing frequently, enables user updates without code change
- [Implementation] Phase-aware token tracking (scout, planning, primary, compaction) — enables cost breakdown, not just total
- [TradeOff] Missing model pricing defaults to zero with warning — no error thrown, helps with incomplete configs

## p13: Ticket Writeback
- [Architecture] Default interface implementations on ITicketProvider — backward compatible, existing providers work without changes
- [Implementation] Fire-and-forget ticket operations wrapped in try/catch — failures never block the pipeline
- [Architecture] CommitAndPRHandler closes ticket and posts summary — provides feedback loop to issue tracker

## p14: GitHub Action & Webhook
- [Architecture] Both GitHub Action (zero infrastructure) and webhook listener (self-hosted) variants — choice for user deployment model
- [Tooling] HttpListener for webhook (no ASP.NET dependency) — minimal overhead for simple HTTP requirements
- [Implementation] Each webhook request spawns async Task — non-blocking, server returns 202 Accepted immediately
- [TradeOff] Trigger label configurable but defaults to "agent-smith" — simplicity over ultra-flexibility

## p15: Azure Repos
- [Architecture] Reuses LibGit2Sharp pattern from GitHub provider — shared git operations, different PR API
- [Implementation] Clone URL embeds PAT for HTTPS auth — consistent with GitHub provider pattern
- [Architecture] PR URL constructed from org/project/repo structure — matches Azure DevOps URL format

## p16: Jira Provider
- [Tooling] No Jira SDK — pure HttpClient with JSON, Jira REST API is straightforward
- [Implementation] ADF (Atlassian Document Format) for comments — required by Jira v3 API, wraps plain text in minimal structure
- [TradeOff] Transition names searched flexibly (contains "Done" or "Closed") — accepts incomplete workflow support over strict matching
- [Implementation] Basic Auth with email:apiToken — standard Jira Cloud authentication method

## p17: GitLab Provider
- [Architecture] Extract `ISourceProvider` and `ITicketProvider` implementations — GitLab MRs use same semantic model as GitHub PRs, REST API v4 is straightforward JSON
- [Implementation] `LibGit2Sharp` for git operations and GitLab REST API v4 for merge requests — consistent with existing GitHub provider

## p18: Chat Gateway
- [Architecture] Redis Streams as message bus (progress/question/done/error) with persistent consumer groups — decouples agent execution from dispatcher, enables multi-platform routing
- [Architecture] Separate `IProgressReporter` implementations: Console for CLI, Redis for K8s — single interface, pluggable output strategy
- [Implementation] K8s Job per request with ephemeral agent containers — isolation, resource limits, TTL cleanup, each fix runs independently

## p19: K8s Deployment
- [Architecture] Multi-stage Dispatcher Dockerfile (build → runtime with non-root user) — separates compile from runtime, reduces image size, hardens security
- [TradeOff] Redis without PersistentVolume initially — ephemeral streams only live during job execution, simplifies local testing, upgrade path exists
- [Architecture] Kustomize base + dev/prod overlays — reusable structure, environment-specific patches avoid duplication

## p19a: Docker Spawner
- [Architecture] Extract `IJobSpawner` interface with Docker and K8s implementations — single abstraction enables local docker-compose mode without K8s
- [TradeOff] Auto-detect Docker network from dispatcher container metadata — avoids explicit config in most cases, fallback to `bridge`
- [Architecture] `AutoRemove=true` on Docker containers — self-cleanup, no orphan accumulation

## p19b: K8s Helm
- [Architecture] Helm Chart replaces manual `apply-k8s-secret.sh` — templated secrets, repeatable deployments, built-in rollback
- [TradeOff] Kustomize kept for reference — users can stick with existing workflow or migrate to Helm incrementally

## p20a: Intent Engine
- [Architecture] Three-stage engine: Regex (free) → Haiku (cheap) → deterministic project resolution — 90% resolved free, fallback only when needed
- [Architecture] Parallel ticket provider queries for project resolution — minimizes latency on multi-project configs
- [TradeOff] Accepted regex brittleness for 90% of inputs to avoid AI cost — only calls Haiku on parse failures

## p20b: Help Command
- [Architecture] Greeting detection in Regex stage — "hi", "hello" cost zero AI tokens
- [Implementation] `ClarificationStateManager` stores low-confidence suggestions in Redis with TTL — avoids second AI call when user confirms

## p20c: Error UX
- [Architecture] `ErrorFormatter` with regex pattern table — maps raw errors to friendly messages, pure function fully testable
- [Architecture] `ErrorContext` carries step number + step name — users see exactly where in the pipeline failure occurred
- [TradeOff] Contact button optional via env var — graceful degradation when `OWNER_SLACK_USER_ID` not set

## p20d: Agentic Detail Updates
- [Architecture] `ReportDetailAsync` on `IProgressReporter` — fire-and-forget so failed detail posts never abort pipeline
- [Implementation] Rate throttling on detail events (max 1 per 3 files) — prevents Slack API rate limit errors
- [TradeOff] Thread ts stored in memory only — Dispatcher restart loses thread continuity but degrades to top-level messages

## p21: Code Quality
- [Architecture] Consistent folder structure (Contracts/, Models/, Services/) across all projects — eliminates sprawl
- [Tooling] `IHttpClientFactory` instead of `new HttpClient()` — proper lifetime management, connection pooling
- [Implementation] Magic numbers → named constants — single source of truth for configuration values

## p22: CCS Auto-Bootstrap
- [Architecture] `ProjectDetector` is deterministic (no LLM, pure file system) — fast, zero cost, fully testable
- [Architecture] Single cheap LLM call for context generation — one-shot avoids iterative refinement, cost ~$0.01
- [TradeOff] Generated `.context.yaml` committed to repo — persistent project context, avoids regeneration on every run

## p24: Code Map
- [Architecture] LLM-assisted extraction instead of custom parsers — hand-written AST parsing is error-prone and language-specific
- [TradeOff] Code map generation only on bootstrap or request — avoids cost on every run, requires explicit regeneration when architecture changes
- [TradeOff] 5,000 token cost per generation but saves 100k+ tokens in file reading — break-even on first agent run

## p26: Coding Principles Detection
- [Architecture] Per-project coding principles loaded at runtime — respects project-specific style, agent sees rules before planning
- [Implementation] Principles as free-form markdown (no schema) — flexibility for humans to define culture-specific rules

## p27: Structured Command UI
- [Architecture] Slack modals with dropdowns replace free-text parsing — no typos, structured data needs no IntentEngine
- [TradeOff] Free-text input kept as fallback — backward compatibility for power users

## p28: .agentsmith/ Directory
- [Architecture] Unified `.agentsmith/` replaces scattered config files — single source of truth for project meta-files
- [Architecture] Run tracking as immutable records (`r{NN}-{slug}/`) — auditable changelog of all agent decisions
- [TradeOff] Failed runs recorded only in runs/ (not state.done) — state.done tracks project state changes, not execution history

## p29: Init Project Command
- [Architecture] Generalize `IJobSpawner.SpawnAsync` from `FixTicketIntent` to generic `JobRequest` — init-project becomes first-class command

## p30: Systemic Fixes
- [Architecture] `CommandResult` carries PR URL through pipeline — URLs were previously lost due to generic return type
- [Architecture] `OrphanJobDetector` as background service monitoring in-memory + Redis — catches orphans from pre-deployment jobs
- [Implementation] Remove `CancellationToken = default` optional parameters — forces explicit cancellation token threading

## p31: Orphan Detector Redis Scan
- [Architecture] `ConversationStateManager.GetAllAsync()` scans `conversation:*:*` Redis keys — detects stale states from before dispatcher restart
- [Implementation] Dual-path detection: in-memory tracked + full Redis scan — comprehensive coverage of all failure modes

## p32: Architecture Cleanup
- [Architecture] `ILlmClient` abstraction wrapping LLM providers — generators become thin prompt-builders, can swap providers without code changes
- [Architecture] `CommandNames` + `PipelinePresets` code-defined — eliminates magic strings, single source of truth
- [TradeOff] Remove language-specific collection code → feed raw files to LLM — less code, more flexible, slightly higher token cost

## p33: Run Cost in Result
- [Architecture] YAML frontmatter in `result.md` with cost breakdown by phase — human-readable and machine-parseable, persists in repo
- [Implementation] `RunCostSummary` moved to Contracts — Application layer can access without Infrastructure dependency

## p34: Multi-Skill Architecture
- [Architecture] Commands insert follow-up commands into flat LinkedList pipeline — no tree structures, fully transparent in logs
- [Architecture] Role-based skills with separate YAML files — pluggable roles, new roles added without code changes
- [Architecture] `ConvergenceCheckCommand` evaluates consensus — prevents infinite discussion loops
- [Safety] Max 100 command executions limit in PipelineExecutor — prevents accidental infinite loops

## p35: Simplified Slack Commands
- [Architecture] Split `FixTicket` into three commands: `FixBug`, `FixBugNoTests`, `AddFeature` — pipeline selection implicit in command type
- [Implementation] `ModalCommandType` enum routes to pipeline presets directly — no separate pipeline dropdown

## p36: GenerateTests & GenerateDocs + Pipeline Resilience
- [Architecture] Per-command exception handling in PipelineExecutor prevents single command crash from failing entire pipeline
- [Architecture] GenerateTests and GenerateDocs execute synthetic plans via IAgentProvider instead of custom logic — reuses agentic loop
- [Architecture] OrphanJobDetector replaced time-based detection with container liveness checking (IsAliveAsync) — eliminates false positives from slow operations

## p37: Strategy Pattern Pipeline Abstraction
- [Architecture] Chose strategy pattern with tell-don't-ask principle — no "if type == coding" anywhere, type resolves correct implementations via DI
- [Architecture] Single class size limit of 120 lines enforced to prevent god objects and improve maintainability
- [Architecture] Renamed pipeline steps (CheckoutSource → AcquireSource, Test → Validate) despite migration burden — reflects true semantics across all pipeline types

## p38: MAD Discussion Pipeline
- [Architecture] SkillsPath moved to ProjectConfig instead of hardcoded path — enables loading from subdirectories for different discussion types
- [Architecture] CompileDiscussionCommand writes discussion transcript instead of executing code steps — reuses existing ConvergenceCheck consolidation

## p39: Legal Analysis Pipeline
- [TradeOff] MarkItDown preprocessing over direct PDF to Claude — accepted token overhead for searchability and consistency across document formats
- [TradeOff] Inbox polling over FileSystemWatcher — chose polling (more stable on Docker bind mounts) over event-based complexity
- [Architecture] LocalFolderSourceProvider and LocalFileOutputStrategy as Pro-only implementations — OSS receives only strategy interfaces

## p40a: Contracts Extensions & ProviderRegistry
- [Architecture] ITypedProvider base interface for all typed providers — eliminates hardcoded factory switches via ProviderRegistry<T>
- [Architecture] AttachmentRef abstraction for all storage sources — supports multiple backends without coupling to specific implementation
- [Implementation] Config passed at construction time (via IOptions<T>) not at factory method call — cleaner DI composition

## p41: Decision Log
- [Architecture] IDecisionLogger with nullable repoPath and tell-don't-ask pattern — handlers always log, implementation decides what to do
- [Implementation] FileDecisionLogger + InMemoryDecisionLogger split — coding pipelines write to file, non-repo pipelines skip silently
- [Architecture] Decisions captured at decision moment (GeneratePlan, AgenticExecute, Bootstrap) not post-processed — ensures accuracy

## p42: Legal Analysis Pipeline Handlers
- [TradeOff] InboxPollingService uses copy-then-delete not move — idempotent on crash, file remains in inbox for retry on next poll
- [Implementation] Contract type detected via Scout (cheap Haiku call) to select correct legal skill subset

## p43a: CI/CD Docker Publish
- [Tooling] Chosen multi-arch build (amd64 + arm64) with QEMU emulation — supports both x86 servers and Apple Silicon developers
- [TradeOff] Stayed on mcr.microsoft.com/dotnet/sdk:8.0 for Host despite larger image — needed for cloned repo build/test execution

## p43b: Security Pipeline
- [Architecture] Chosen IPrDiffProvider interface over PR-specific methods — enables diff-only scan without full checkout
- [Tooling] SecurityScan pipeline preset reuses Triage + ConvergenceCheck from discussion pattern — no duplicated state management
- [Architecture] DeliverOutputCommand with IOutputStrategy keyed services (.NET 8) — pluggable output backends without factory methods

## p43c: SARIF Output Strategy
- [Architecture] IPrCommentProvider injected separately into SarifOutputStrategy — decouples platform-specific comment posting from finding analysis
- [Implementation] Skill output requires structured format (file/line/severity/confidence) instead of prose — enables SARIF mapping without LLM re-parsing

## p43d: Ollama Local Model Support
- [Tooling] OpenAiCompatibleClient shared between OpenAI and Ollama via composition not inheritance — avoids class hierarchy complexity
- [TradeOff] Structured text fallback when tool calling unavailable — preferred over forcing minimum model capability
- [Implementation] ModelAssignment extended with ProviderType and Endpoint (both nullable) — backward compatible with existing single-provider configs
- [Architecture] Startup validation pings Ollama endpoint and detects tool calling capability — fails fast with clear error messages

## p43e: Webhook Expansion
- [Architecture] Replaced monolithic WebhookListener with IWebhookHandler dispatch pattern — eliminates large switch statement, enables platform handlers in isolation
- [Architecture] SecurityScanRequest as common mapping target across all platforms — decouples platform-specific payloads from execution logic

## p44: Rename ProcessTicket → ExecutePipeline
- [Architecture] Renamed ProcessTicketUseCase to ExecutePipelineUseCase — reflects that system orchestrates arbitrary workflows, not just tickets

## p45: API Security Scan Pipeline
- [Architecture] Reused TriageHandlerBase and SkillRoundHandlerBase from p43b instead of duplicating — established pattern for cascading commands
- [Tooling] Chose Nuclei over manual HTTP scanning — handles authentication, redirects, and HTTP semantics that would require custom crawling

## p46: CLI Refactor & LLM Intent Parsing
- [Architecture] CLI changed to explicit flags (agent-smith fix --ticket 42) instead of free-text parsing — self-documenting and extensible
- [Tooling] Replaced regex-based intent parsing with Haiku LLM parsing for Slack — handles natural language variations, eliminates pattern fragility
- [Architecture] Collapsed ExecuteTicketAsync/ExecuteTicketlessAsync/ExecuteInitAsync into ExecuteAsync(PipelineRequest) — single code path regardless of source
- [Implementation] Program.cs split from 456 to <30 lines — each command handler under 120 lines, extracted Banner and ServiceProviderFactory

## p47: API Contract & Schema Analysis
- [Architecture] Spectral runs as second static analyzer alongside Nuclei — complements mechanical scan with structural validation
- [Tooling] api-design-auditor rewritten with full schema analysis — semantic layer that tools cannot reach (sensitive data bundling, enum opacity, REST semantics)
- [Implementation] Spectral findings feed same skill pipeline as Nuclei — unified analysis despite different scanner types

## p48: Swagger Context Compression
- [Architecture] Compression happens in handler (ApiSkillRoundHandler.BuildDomainSection) not as pipeline command — formatting concern, not workflow step
- [TradeOff] Deduplication + schema reference strategy over raw swagger.json — accepted 85% token reduction over complete fidelity

## p49: Tool Runner Abstraction
- [Architecture] IToolRunner abstraction with multiple implementations (Docker/Podman/K8s/Process) — single interface handles all deployment modes
- [TradeOff] Spawners hardcode container paths (currently hardcoded, improved in p51) — simplifies path handling despite DockerToolRunner complexity

## p50: Multi-Output Strategy
- [Architecture] --output accepts comma-separated values, multiple IOutputStrategy implementations run once — avoids duplication but enables customized output
- [Implementation] SummaryOutputStrategy for clean findings view — filters skill discussion noise, shows only retained findings grouped by severity
- [Implementation] OutputDir resolved once in handler not per-strategy — eliminates per-strategy fallback chains

## p51: ToolRunner Clean Architecture
- [Architecture] ToolRunRequest uses {input}/{output} placeholders resolved per-runner — logical I/O decoupled from execution model paths
- [Implementation] Each runner translates placeholders to its model (/work for Docker, tempdir for Process, /work for K8s) — clean separation of concerns
- [TradeOff] No trimming for single executable — reflection-heavy dependencies (YamlDotNet, Docker.DotNet) make trimming unstable

## p52: Single Executable Release
- [Tooling] PublishSingleFile + SelfContained without trimming — ~70-80MB binaries acceptable, avoids reflection breakage
- [Architecture] Config file discovery chain (.agentsmith/, ./config/, ~/.agentsmith/) — no hardcoded paths, works across deployment contexts
- [Implementation] Docker entrypoint script fixes mount permissions and drops to agentsmith user — eliminates permission issues without requiring consumer setup

## p53: Documentation Site
- [Architecture] MkDocs + Material theme over custom solution — reduces maintenance burden, leverages community defaults
- [Architecture] README trimmed to landing page, docs site as authoritative source — solves discoverability and information architecture split
- [Architecture] GitHub Pages deployment over managed hosting — free, version-controlled docs that stay with the code

## p54: Security Scan Expansion
- [Architecture] Deterministic tools (regex, git history, dependency auditing) precede LLM skills — improves recall on pattern-based findings, reduces token cost while maintaining context awareness
- [Tooling] YAML pattern files are user-extensible without code changes — enables users to add project-specific patterns without rebuilding
- [Tooling] Separate tool commands (StaticPatternScan, GitHistoryScan, DependencyAudit) over monolithic "scanner" — each can be skipped independently based on repo type
- [TradeOff] Pattern-based pre-scanning accepted higher false positives over perfect recall — LLM specialists later filter and contextualize

## p55: Security Findings Compression
- [Implementation] Top-N detail + remainder summary per category — reduces token cost 70%+ while keeping findings accessible to relevant skills
- [Architecture] Category-based slicing to skills instead of full findings dump — each skill only receives data it can act on
- [TradeOff] Compressed threshold (≤15 findings = full detail) over uniform compression — small categories skip compression to preserve context

## p56: Security Scan Polish
- [Implementation] Mandatory false-positive-filter always runs regardless of triage — ensures consistency, prevents missed filtering
- [Architecture] ExtractFindingsCommand unifies security-scan and api-scan delivery paths via SARIF — consistent output format across pipelines
- [Tooling] SecretProviderRegistry maps patterns to revoke URLs without live API probing — actionable for developers without runtime risk

## p57a: Skill Standard
- [Architecture] Three-file skill structure (SKILL.md + agentsmith.md + source.md) over single YAML — separates portable skill content from Agent-Smith extensions and provenance
- [TradeOff] Local skills require source.md with origin=local — enforces explicit knowledge of whether skills are internal or external

## p57b: Skill Manager Pipeline
- [Implementation] Human approval mandatory before skill installation — treats skills as code injection, no automated installation
- [Architecture] SKILL.md never modified, extensions in agentsmith.md only — preserves upstream skill integrity, enables future updates
- [TradeOff] Pinned versions in source.md over automatic updates — requires explicit review cycle for new skill versions

## p57c: Autonomous Pipeline
- [Architecture] Agent writes tickets, human decides whether to act — inverts control: agent prioritizes, human validates rather than human tasking agent
- [Implementation] Three-part ranking from convergence (not exhaustive list) — avoids ticket spam, forces prioritization through multi-skill agreement
- [TradeOff] project-vision.md must exist before autonomous runs — human knowledge about what matters cannot be inferred from code alone

## p58: Interactive Dialogue
- [Architecture] Typed DialogQuestion (Confirmation, Choice, FreeText, Approval, Info) over binary Yes/No — supports nuanced human input without proliferating new adapters
- [Implementation] Dialogue trail always in result.md, never lost — audit trail of all agent-human interaction tied to the run
- [TradeOff] Timeout defaults to sensible value rather than infinite wait — CLI/PR workflows don't block forever without human
- [Implementation] Agent has ask_human tool with system prompt rules about when to ask — prevents overuse while allowing genuine clarifications

## p58b: Microsoft Teams Integration
- [Implementation] Adaptive Cards for all 5 QuestionType variants — Teams-native UI that preserves same dialogue model across all adapters
- [TradeOff] Removed old AskQuestion method entirely once all adapters migrated — cleaner contract, forces consistency

## p59: PR Comment as Input
- [Architecture] Unified webhook dispatch pattern (IWebhookHandler) handles both GitHub Issues and PR Comments — one entry point scales to multiple event types
- [Implementation] CommentIntentRouter distinguishes new job vs. dialogue answer at parse time — same Redis infrastructure serves both scenarios
- [Architecture] Platform-agnostic CommentIntent model, platform-specific webhook handlers — enables p59b/p59c without changing routing logic
- [TradeOff] Sparse checkout + no container job for PR context vs. full agent container — trade implementation simplicity for PR-specific context loading

## p59b: GitLab MR Comments
- [Implementation] GitLab webhook handler added, CommentIntentRouter reused — confirms separation of platform-specific handling from intent logic

## p59c: Azure DevOps PR Comments
- [Implementation] AzDO webhook handler added, CommentIntentRouter reused — demonstrates platform extension pattern scales

## p60: Security Pipeline Enhancements
- [Architecture] DAST via ZAP after static tools, not integrated into static — runtime testing reveals issues invisible in code, separate tool appropriate
- [Implementation] Auto-fix spawns separate K8s jobs, doesn't block security scan — security findings delivered immediately, fixes continue asynchronously
- [TradeOff] Git-based trend analysis over external database — leverages YAML frontmatter + git history, no persistence layer
- [Implementation] Trend committed to default branch, not PR branch — accumulated knowledge stays project-wide, avoids merge conflicts

## p61: Project Knowledge Base
- [Architecture] wiki/ compiled incrementally by LLM from runs/ + security/ — Karpathy pattern: no embeddings, no vectors, human-readable markdown
- [Implementation] CompileKnowledge respects rate limiting (default 1x/hour) — prevents token waste on frequent small runs
- [Architecture] QueryIntent requires @agent-smith prefix — prevents collision with ticket descriptions that start with questions
- [Implementation] Wiki queries use sparse checkout of .agentsmith/wiki/ only — 5-15 seconds instead of minutes, no container job needed
- [TradeOff] Decisions compiled from multiple runs, not just latest — institutional memory accumulates; contradictions detected by WikiLint

## p62: Docs Site Update
- [Implementation] Docs updated after p58–p61 complete — ensures documentation describes implemented features, not planned ones

## p63: Structured Finding Assessment
- [Implementation] All findings reach skills (remove Top-N cap), compression changes formatting not visibility — every finding gets at least one-line assessment
- [Architecture] Finding record gains ReviewStatus (not_reviewed | confirmed | false_positive) — tracks agent assessment, enables filtering
- [Implementation] Convergence produces JSON assessments alongside prose — structured output that actually flows into DeliverFindings
- [TradeOff] Accepted ~3-4k token increase per skill call vs. hidden findings that skills never see — visibility trumps compression

## p64: Typed Skill Orchestration
- [Architecture] Pipeline type declared (discussion | structured | hierarchical) → determines orchestration model — one model doesn't fit all
- [Implementation] SkillGraphBuilder replaces LLM-based Triage for structured/hierarchical — deterministic execution graph from skill metadata, no LLM call needed
- [Architecture] Contributors receive only their input_categories slice, run in parallel independently — 80% token reduction vs. free-form discussion
- [Implementation] Gate role produces structured JSON (confirmed | rejected) instead of free-text discussion — enables filtering to actually affect output
- [TradeOff] Fallback to discussion model (contributor role) for skills without orchestration block — backward compatible, new pipelines get defaults

## p65: Website Redesign
- [Architecture] CSS-only redesign, no template changes — isolates visual changes from site structure
- [Implementation] Shadow-as-border (1px rgba border) over CSS borders — aligns with Vercel/Geist aesthetic while maintaining simple HTML
- [TradeOff] Three font weights only (400/500/600) over full spectrum — constrains design but reduces font loading, enforces visual hierarchy

## p66: Docs Enhancement — Self-Documentation & Multi-Agent Orchestration
- [Architecture] DESIGN.md placed in docs/ not project root — it is a docs-site concern, not product code
- [Tooling] CSS-only theme overrides via extra_css, no custom MkDocs templates — keeps MkDocs upgrades safe
- [TradeOff] Content first, styling second — missing content is a blocker, imperfect styling is not
- [Implementation] Reuse existing fix-and-feature.md instead of creating separate fix-bug.md — page already covers both pipelines

## p67: API Scan Compression & ZAP Fix
- [Architecture] Category slicing (auth/design/runtime) instead of finding compression — findings are already compact at ~90 chars/piece, compression would lose information. Slicing routes findings to the right skill without data loss.
- [Tooling] WorkDir as optional ToolRunRequest parameter instead of Docker volume mounts — volume mounts would add complexity to DockerToolRunner. WorkDir + tar extraction to / is simpler and backward compatible (Nuclei/Spectral unaffected).
- [Implementation] Inject target URL into swagger servers[] instead of pinning ZAP version — ZAP needs absolute URLs, many OpenAPI specs only have relative "/". Patching the spec before copy is non-invasive.
- [TradeOff] Remove --auto flag entirely instead of finding replacement — --auto was never a valid option on ZAP's Python wrapper scripts. The scripts are non-interactive by default in Docker containers.
- [Implementation] Skip DAST skills on ZAP failure via ZapFailed flag — avoids wasting 2 LLM calls on empty input. Flag is checked in ApiSecurityTriageHandler before building the skill graph.

## p68: API Finding Location
- [Architecture] DisplayLocation as computed property on Finding record — no new field in serialization, just display logic. Fallback chain: ApiPath > SchemaName > File:StartLine.
- [TradeOff] Nullable fields instead of separate ApiFinding subtype — keeps one Finding type across all pipelines. Security-scan findings simply leave ApiPath/SchemaName null.
- [Implementation] NullIfEmpty normalization in ParseGateFindings — LLMs return empty strings instead of omitting fields. Normalize at parse time and defend in DisplayLocation with IsNullOrWhiteSpace.

## p70: Decision Log — Phase/Run Context
- [Architecture] sourceLabel as section header instead of category — the real lookup key is "which phase made this decision", not "what kind of decision was it". Category preserved as inline tag.
- [TradeOff] Optional parameter instead of new overload — keeps backward compat, callers that don't know their phase context still work with category-only sections.
- [Implementation] Ticket ID as sourceLabel in pipeline handlers — GeneratePlanHandler and AgenticExecuteHandler pass #{ticketId} so run decisions land under their ticket.

## p71: Jira Assignee Webhook Trigger
- [Architecture] Handler returns WebhookResult(TriggerInput, Pipeline) instead of enqueueing jobs directly — follows existing dispatch pattern where WebhookListener calls ExecutePipelineUseCase. Phase doc proposed IJobEnqueuer; actual codebase delegates execution to the Listener.
- [Architecture] ServerContext record for config path DI — handler needs config for assignee matching and label→pipeline resolution, but IWebhookHandler.HandleAsync has no configPath parameter. Introduced ServerContext(ConfigPath) registered at server startup, injected into handler. Minimal surface, no interface changes.
- [Security] Secret configured + signature header missing → reject — phase doc originally returned true (skip), which would let attackers bypass verification by omitting the header. Fixed to return false.
- [Implementation] Config-order priority for label→pipeline resolution — iterate PipelineFromLabel keys (user-defined order) instead of payload labels (Jira-side order is undocumented and non-deterministic). Gives users explicit control over priority.
- [Implementation] WebhookListener extracts webhookEvent from Jira payload, strips "jira:" prefix → eventType for CanHandle — Jira has no event-type header unlike GitHub/GitLab. Listener parses once before dispatch.
- [TradeOff] Jira signature validation loads config per request instead of caching — keeps validation consistent with runtime config changes. Acceptable cost for webhook traffic volume.
- [Scope] Unassign scenario (Agent Smith removed from ticket) explicitly out of scope — handler returns Ignored correctly, but cancelling a running job requires Job Cancellation feature that doesn't exist yet.
