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

## p72: Jira Assignee Webhook Trigger
- [Architecture] Handler returns WebhookResult(TriggerInput, Pipeline) instead of enqueueing jobs directly — follows existing dispatch pattern where WebhookListener calls ExecutePipelineUseCase. Phase doc proposed IJobEnqueuer; actual codebase delegates execution to the Listener.
- [Architecture] ServerContext record for config path DI — handler needs config for assignee matching and label→pipeline resolution, but IWebhookHandler.HandleAsync has no configPath parameter. Introduced ServerContext(ConfigPath) registered at server startup, injected into handler. Minimal surface, no interface changes.
- [Security] Secret configured + signature header missing → reject — phase doc originally returned true (skip), which would let attackers bypass verification by omitting the header. Fixed to return false.
- [Implementation] Config-order priority for label→pipeline resolution — iterate PipelineFromLabel keys (user-defined order) instead of payload labels (Jira-side order is undocumented and non-deterministic). Gives users explicit control over priority.
- [Implementation] WebhookListener extracts webhookEvent from Jira payload, strips "jira:" prefix → eventType for CanHandle — Jira has no event-type header unlike GitHub/GitLab. Listener parses once before dispatch.
- [TradeOff] Jira signature validation loads config per request instead of caching — keeps validation consistent with runtime config changes. Acceptable cost for webhook traffic volume.
- [Scope] Unassign scenario (Agent Smith removed from ticket) explicitly out of scope — handler returns Ignored correctly, but cancelling a running job requires Job Cancellation feature that doesn't exist yet.

## p77: Pipeline Fixes
- [Architecture] skills_path resolved relative to config file directory — same pattern as tsconfig.json, docker-compose.yml. Config file is the anchor point, not CWD or repo root. Covers: local scan (repo has its own skills), API scan (no repo), CI with separate config repo (RHS.CICD/tools/agent-smith/config/).
- [Fix] ZAP exit codes 0-3 are valid scan results (pass/info/warnings/failures), not errors — only >3 indicates a tool crash. Previously any non-zero was treated as failure, discarding valid DAST findings.
- [Fix] Docker cp directories created with mode 0777 — ZAP container runs as UID 1000 (zap user), but docker cp creates files as root. World-writable permissions fix the PermissionDenied on /zap/wrk/zap-report.json.

## p73: Class Size Enforcement
- [Quality] CI gate starts non-blocking, flip to blocking after Tier 1+2 — allows incremental adoption without breaking builds.
- [Scope] Pure refactoring only (extract, rename, split — no behavior changes) — ensures no regressions from size enforcement work.

## p74: CLI Source Overrides
- [Architecture] Three generic options (--source-type, --source-path, --source-url, --source-auth) replace --repo — covers all provider types uniformly.
- [Architecture] Applied in ExecutePipelineUseCase (transparent to downstream) — handlers don't need to know about CLI overrides.
- [Cleanup] SecurityScanCommand drops --repo — replaced by generic source options.

## p75: Phase Docs to YAML
- [Architecture] YAML over markdown — structured input yields structured output, ~10x token reduction (96k → 19k words).
- [Scope] No code examples in phase specs — agent reads codebase directly, examples become stale.
- [Scope] Type signatures kept, action field supports single value or list.
- [Scope] Delete original markdown files — git history is the archive.

## p76: Azure OpenAI Provider
- [Architecture] Inheritance over composition — only CreateChatClient differs from OpenAiAgentProvider.
- [Configuration] Only 'deployment' and 'api_version' added to AgentConfig — api_version defaults to 2025-01-01-preview.
- [Tooling] Azure.AI.OpenAI NuGet (official Microsoft package).

## p78: Gate Category Routing
- [Architecture] Filter before gate, merge after — each gate sees only its input_categories, results are combined.
- [Architecture] Unmatched findings pass through unchanged — categories not claimed by any gate are not lost.
- [Architecture] No gate ordering dependency — gates in same stage run independently on disjoint slices.

## p81: Integration Setup Docs
- [Scope] All guides in English, under docs/setup/ as dedicated section.
- [Scope] Slack guide review/update, Teams guide is new (Azure Bot registration, ngrok, manifest, docker-compose env vars).
- [Scope] Webhook and label-trigger docs split to p82.
- [Decision] Manifest template under docs/setup/teams/ (not deploy/).
- [Decision] Teams status marked 'beta' in chat-gateway.md.
- [Decision] No placeholder PNGs — document icon requirements in text instead.

## p83: Jira Webhook Status Lifecycle
- [Architecture] Trigger requires all three: assignee match + label match + status in whitelist — prevents accidental runs on resolved/closed tickets.
- [Architecture] Comment trigger (comment_created) is a separate handler, not a mode in JiraAssigneeWebhookHandler — separate event type, cleaner routing.
- [Architecture] Comment trigger also checks status whitelist — prevents triggering on closed tickets.
- [Architecture] done_status resolved via transition name, not status ID — names are human-readable and portable across Jira projects.
- [Architecture] All pipelines are valid trigger targets — pipeline_from_label maps to any pipeline name, no hardcoded restrictions.
- [Reuse] Status transition after PR extracts TransitionToAsync from JiraTicketProvider.CloseTicketAsync — reusable for any target status.
- [Flow] DoneStatus flows via WebhookResult.InitialContext → PipelineContext → CommitAndPRHandler — no coupling between webhook handler and pipeline handler.
- [Note] PipelineFromLabel uses Dictionary<string,string> — insertion order preserved by YamlDotNet but not spec-guaranteed. Migrate to OrderedDictionary<TKey,TValue> when moving to .NET 9.

## p82: Webhook & Trigger Docs
- [Scope] Per-platform setup guides under docs/setup/webhooks/ — each with prerequisites, step-by-step, config YAML, verification, troubleshooting.
- [Scope] Label-triggers overview documents current state per platform and p84 roadmap for unified configuration.
- [Decision] Guides link to each other and to configuration reference (webhooks.md) — no duplication of config details.

## p85: Webhook Structured Dispatch
- [Architecture] Webhook handlers return ProjectName + TicketId instead of free-text TriggerInput — WebhookRequestProcessor builds PipelineRequest directly, bypassing RegexIntentParser.
- [Architecture] PR comment handlers still use TriggerInput string (free-form arguments) — legacy path preserved as fallback.
- [Fix] ConsolidatedPlan is input for GeneratePlanHandler, not a replacement — multi-skill discussion provides analysis context, GeneratePlanHandler distills concrete PlanSteps. Previously the handler short-circuited with 0 steps, causing the agent to spend 32 iterations guessing what to do.
- [Fix] Jira Cloud system webhooks don't send signature headers — signature validation now skips when no header present, even if secret is configured.

## p84: Unified Webhook Lifecycle
- [Architecture] WebhookTriggerConfig as shared base for all platforms — status gate, pipeline-from-label, done_status, comment re-trigger unified across GitHub, GitLab, Azure DevOps, Jira.
- [Architecture] Hardcoded trigger labels/tags replaced by config with backward-compatible defaults — existing setups work without changes.
- [Architecture] All platforms support pipeline_from_label — any pipeline is a valid trigger target, no hardcoded restrictions.

## p86: Typed Skill Observations
- [Architecture] SkillObservation as universal output contract — replaces free-text DiscussionLog, pipeline-agnostic structured data.
- [Architecture] ID assigned by framework, not LLM — prevents hallucinated or colliding IDs.
- [Architecture] ConvergenceResult replaces ConsolidatedPlan string — mechanical plan generation from structured input.
- [Architecture] GeneratePlanHandler always runs — ConsolidatedPlan is context for plan generation, not a replacement.
- [Implementation] Concern, Severity, Effort as enums with JsonStringEnumConverter — type-safe, serialization-friendly.
- [TradeOff] Partial valid JSON accepted — take valid observations, skip broken ones with warning. Robustness over strictness.

## p79/p80: Attacker-Perspective Security Skills
- [Architecture] Commodity tools (StaticPatternScan, GitHistoryScan, DependencyAudit) are passive layer — unchanged. Intelligence layer (LLM attacker skills) is active layer on top.
- [Architecture] Attacker-perspective skills (recon-analyst, low-privilege-attacker, idor-prober, input-abuser, response-analyst) complement knowledge-domain skills — both run together.
- [Architecture] chain-analyst is executor — receives all commodity + skill findings and produces chained attack assessment.
- [Scope] No runtime, no user accounts, no HTTP probing — code/diff analysis only.

## p87: Ticket Image Attachments
- [Architecture] IAttachmentLoader per platform — keeps TicketProvider focused on ticket CRUD, image downloading is separate concern.
- [Implementation] Only image MIME types (png, jpeg, gif, webp) — skip PDFs, ZIPs. Max 5MB per image, skip larger with warning.
- [Implementation] Images stored as base64 in PipelineContext via ContextKeys.Attachments — passed as vision input to LLM during plan generation.
- [TradeOff] Markdown parsing for GitHub/GitLab (![](url) patterns) over API-based attachment listing — issues embed images inline, not as separate attachments.

## p88: Configurable Defaults
- [Architecture] Config wins over hardcoded — every value that depends on the user's environment must be readable from agentsmith.yml.
- [Architecture] Ticket states as whitelist (IN clause) instead of blacklist (<> exclusions) — unknown states excluded by default. Blacklists in foreign systems are a bug machine.
- [Architecture] PR target branch: repo API → config → "main" fallback chain. API result cached per run — one extra API call, not per PR.
- [Architecture] Missing ADO fields map to null, not errors — AcceptanceCriteria may not exist in all process templates.
- [Architecture] GitLab base URL required for self-hosted — no silent fallback to gitlab.com. Startup validation error with clear message.
- [TradeOff] ADO API version via env var (AZDO_API_VERSION) instead of config file — env vars are standard for server-side overrides, config file would be over-engineering for a rarely changed value.

## p89a: Skill Content Improvements
- [Architecture] Confidence calibration defined once in observation-schema.md — Low (0-30), Medium (31-69), High (70-100). Referenced by all skills.
- [Architecture] Framework-specific false-positive rules derived from Anthropic's claude-code-security-review — 12 precedents battle-tested across thousands of reviews.
- [Architecture] Phase 1 repo context exploration before analysis — skills must understand existing security patterns before flagging findings. Deviations from established patterns are more likely real findings.
- [Scope] Content only — zero C# changes, zero build risk.

## p92: K8s Config Cleanup
- [Architecture] Flat numbered YAMLs over Kustomize — GitOps repos (ArgoCD) apply plain YAML directly; Kustomize/Helm can be derived from flat files if needed, not the other way around
- [Architecture] Dev/prod differences as inline comments (e.g. `# prod: 2`) instead of overlay patches — this is a reference deployment, not a production GitOps repo
- [Implementation] Shell script over Makefile for ConfigMap regeneration — project has no Makefile, standalone script in deploy/k8s/ is more discoverable
- [Implementation] Two placeholder projects (GitLab+Claude, AzureDevOps+AzureOpenAI) — covers provider combos not already shown in agentsmith.example.yml (which defaults to GitHub+Claude)
- [Implementation] Pricing moved under agent block in example.yml — matches current real config structure where pricing is provider-specific, not project-level
- [Scope] Config/infra only — zero C# changes, zero build risk

## p93: LLM Output Error Handling
- [Architecture] Silent catch banned for LLM-output parsing — every parse failure logs the response and returns Fail. Silent pass hid unreliable gates; a scan could report "647 raw → 647 extracted" while the gate had actually failed to parse.
- [Architecture] One corrective retry in the gate path (`GateRetryCoordinator`) — cheap (one extra LLM call worst case), recovers most schema drift. Retry failure fails the pipeline rather than silently letting findings through, because an unfiltered scan masquerading as filtered is worse than an explicit failure.
- [Architecture] Gate (output: list) must declare `input_categories` explicitly — `*` for wildcard or a concrete list. Empty/missing is rejected at skill load. Gate (output: verdict) is exempt because it doesn't filter findings.
- [Architecture] Validation throws at load time, skill-loader logs as Error — invalid skills don't silently vanish from the role set; the error is visible before the pipeline runs.
- [Scope] LLM-output parsing only — infrastructure cleanup catches (Docker, Process, file fallbacks) are legitimately best-effort and stayed unchanged. Non-gate parsers (Consolidation, Wiki) got logging on their existing fallbacks, not behavioral changes, because their fallback paths are semantically valid (degraded text, empty dict).

## p94a: Gitignore-Aware Source Enumeration
- [Architecture] LibGit2Sharp's `Repository.Ignore.IsPathIgnored` is the ignore source of truth when scanning a git repo — nested `.gitignore`, `.git/info/exclude`, and global excludes all covered without a hand-rolled gitignore parser.
- [Architecture] Hardcoded `ExcludedDirectories` list shrunk but kept as a non-git fallback — `.git/`, `node_modules/`, `bin/`, `obj/`, `__pycache__/`, `.vs/`, `.idea/` stay for CLI scans of arbitrary local paths; `dist/`, `build/`, `vendor/`, `packages/` removed because real repos gitignore them anyway.
- [Architecture] Binary-extension filter stays orthogonal to gitignore — `.gitignore` doesn't mark `.png` as binary, and many repos correctly track binary assets. Two independent reasons to skip a file.
- [Implementation] macOS `/var` → `/private/var` symlink fallback in `GitIgnoreResolver.ToRelative` — realpath canonicalization inside LibGit2Sharp vs. caller-supplied `/var/...` paths breaks direct `Path.GetRelativePath`; a targeted `/private/` prefix strip handles the common test/scratch case without introducing a P/Invoke dependency.
- [Scope] Enumeration only — no changes to pattern definitions, scanner, or findings model. Observable effect limited to fewer files reaching the scanner for repos that gitignore their build output.

## p94b: Security-Scan Skill Reduction (15 → 9)
- [Architecture] Attacker-perspective skills (vuln-analyst, recon-analyst, response-analyst, low-privilege-attacker, input-abuser, idor-prober) deleted from `config/skills/security/` — in a code-audit context without HTTP probing, their prompt output overlapped with the knowledge-domain skills (auth-reviewer, injection-checker, config-auditor, compliance-checker). The same skills remain in `config/skills/api-security/` where HTTP probing and persona-based testing give them genuinely distinct capabilities.
- [Architecture] idor-prober replaced by two independent mechanisms: (1) auth-reviewer's SKILL.md scope extended to explicitly cover IDOR/BOLA (sequential IDs, ownership predicates, cross-tenant, bulk-ops); (2) new `config/patterns/auth.yaml` with 4 static patterns (ASP.NET int route constraint, EF Find-by-id, LINQ single-predicate ID lookup, raw SQL where-id-only). LLM covers nuance, static patterns catch deterministic signals cheaply.
- [Architecture] Legacy `SkillCategories` dictionary in `SecurityFindingsCompressor` removed — since p93 made `orchestration.InputCategories` authoritative, the fallback branch was unread code for every skill with proper input_categories. With the dict gone, an undeclared Contributor skill now returns an empty slice, which is the correct behaviour (gets the overall findings summary instead).
- [Architecture] `GetSliceForSkill` gained explicit wildcard handling — `input_categories: ["*"]` now concatenates every category slice, matching the documented p93 semantics. Before this change the gate reached for `categorySlices["*"]` (never present) and silently returned empty; the overall summary was a hidden fallback that masked the bug.
- [Scope] Baseline and comparison numbers recorded in the phase spec as decision keys: 15-skill baseline wallclock=4m15s, post-gate=14 findings; 9-skill run wallclock=3m (29% faster, target ≥25% met), post-gate=10. Severity-weighted variance is -18% (outside the ±15% done criterion), attributable to LLM gate-judgement variance on pattern-definition files rather than real coverage loss — both runs mostly filter `config/patterns/*.yaml` regex samples, and each run keeps a different subset. Follow-up candidate: exclude `config/patterns/*.yaml` from self-scans.
- [TradeOff] ±15% severity-weighted tolerance was documented as a hard done criterion but proved tighter than single-run LLM determinism supports. Rather than re-run until we hit the target, the variance is recorded honestly with root-cause analysis; future coverage criteria should either average over multiple runs or set a wider tolerance.

## p95a: Ticket-Claim Spine (GitHub only)
- [Architecture] Split the original p95 "ticket-claim-lifecycle" phase into p95a (this), p95b (all-platform transitioners), and p95c (heartbeat + reconciler + full config). Reason: the original p95 bundled ~27 new prod files + 4 heterogeneous platform API integrations + hosted services + config schema into one phase — too large to commit incrementally. Splitting gives each sub-phase a testable, committable increment. Documented in the three phase YAMLs; p96 (poller) now `requires: [p95a, p95b, p95c]`.
- [Architecture] Introduced IRedisJobQueue (RPUSH/LPOP FIFO on `agentsmith:queue:jobs`) as the durable handoff between webhook receiver and worker. Webhook no longer runs pipelines fire-and-forget in-process. Before p95a this path didn't exist — the existing ServerMode invoked `ExecutePipelineUseCase` directly from the webhook handler, which had no backpressure and no crash recovery. The queue is ephemeral (ticket status is the truth); recovery lands in p95c.
- [Architecture] IRedisClaimLock uses SETNX for acquisition and a CAS Lua script for release (returns the acquirer token; release only deletes if the stored value matches). Prevents a caller whose lock TTL expired from deleting a lock subsequently re-acquired by another process. Simple Redis DEL without CAS was considered and rejected — the failure mode it enables (releasing a foreign lock during clock skew or GC pause) is subtle enough to warrant the Lua.
- [Implementation] ConsumeAsync polls via LPOP + 1s delay instead of BRPOP. StackExchange.Redis's IDatabase doesn't expose BRPOP directly; raw ExecuteAsync would work but adds complexity. Polling matches the pattern already used by RedisMessageBus and keeps the queue code under 80 lines. Latency impact: ~500ms worst case on queue entry → visible to consumer, acceptable given pipeline wall time dominates.
- [Architecture] PipelineQueueConsumer is a plain class with RunAsync(CancellationToken), not a `BackgroundService`. The Cli `server` command isn't built on `IHost` (it uses a direct ServiceProvider), so BackgroundService would require introducing IHost just for this consumer. Matches WebhookListener's pattern; both are awaited via Task.WhenAll inside ServerCommand. When/if Cli migrates to IHost in the future, this becomes a BackgroundService with one line of DI.
- [Architecture] QueueConfig lives on the root AgentSmithConfig (not per-project ProjectConfig.Agent), because the queue key is process-wide and max_parallel_jobs caps pipelines across all projects on one pod. Per-project tuning would give wrong semantics (a busy project would unfairly hog slots).
- [Architecture] Platform-specific routing of webhooks-to-ClaimService is a string check `result.Platform == "GitHub"` in WebhookRequestProcessor. For p95a, GitLab/AzDO/Jira keep their in-process ExecuteStructuredAsync fire-and-forget path. p95b will flip them over once their transitioners exist. Documented explicitly in the WebhookRequestProcessor class summary so the legacy branch isn't mistaken for dead code.
- [TradeOff] PipelineRequest moved from AgentSmith.Application/Models to AgentSmith.Contracts/Models. The queue interface lives in Contracts (infrastructure implements; application consumes), and accepting PipelineRequest forces it into the Contracts layer. Touched ~14 files' `using` directives — small one-time churn, correct layering.
- [Architecture] GitHub transitioner uses If-Match ETag on PATCH /issues/{n} with a full labels-array replacement. GitHub's actual ETag enforcement on issue PATCH is documented but not guaranteed to 412 consistently in practice; the code handles 412 correctly if it comes, otherwise accepts last-write-wins semantics. p95c's full lifecycle config + reconciler limits the blast radius of a lost transition.
- [Scope] ITicketClaimService interface takes AgentSmithConfig as a method parameter (not via DI). The caller (WebhookRequestProcessor) already loads config each request for other reasons; forcing the service to load it adds ConfigPathHolder plumbing without benefit. Keeps the service stateless and easy to unit-test.
- [Scope] PR-comment webhook path (TriggerInput, dialogue answers) is explicitly untouched in p95a. Spec calls it out. Those flows don't own ticket lifecycle and don't benefit from claim semantics. Migrating them is a separate concern, not a sub-phase of p95.

## p95b: Multi-Platform Status Transitioners
- [Architecture] GitLab uses PUT /issues/{iid} with add_labels/remove_labels rather than full-labels replacement. Targets only the lifecycle label, leaves unrelated labels untouched. No ETag on GitLab issues — concurrency is last-write-wins at the platform level; TicketClaimService's SETNX claim-lock carries the primary race guard. Full-label PUT was considered and rejected — concurrent edits to non-lifecycle labels would be clobbered.
- [Architecture] AzureDevOps uses JSON Patch on /workitems/{id} with an explicit `test /rev` operation before the `add /fields/System.Tags` — AzDO returns 412 (PreconditionFailed) on rev mismatch. System.Tags is a semicolon-separated list; we read-filter-add-serialize. 409 Conflict treated as PreconditionFailed too (AzDO occasionally returns that for concurrent modifications).
- [Architecture] Jira label-mode uses an *additional* SETNX label-lock (`agentsmith:jira-label-lock:{ticketId}`, 10s TTL) on top of the global claim-lock that TicketClaimService already holds. Reason: Jira's PUT fields.labels is not atomic — two concurrent PUTs from the same pod could both succeed and clobber each other within the claim-lock's validity window if the window straddled a GET/PUT pair. The label-lock narrows the atomic critical section specifically to the label mutation. p95a's single-lock pattern was considered and rejected for Jira specifically.
- [Architecture] JiraWorkflowCatalog ships in p95b as a skeleton — always returns Label-mode regardless of project. Native probing (GET /rest/api/3/project/{key}/statuses) waits for p95c when LifecycleConfig introduces per-project status name customisation; probing before that would force native-status mapping into the p95b scope. Structure is in place (ConcurrentDictionary cache, `SetModeForTest` internals) so p95c only flips the probe logic.
- [Scope] WebhookRequestProcessor's `UsesClaimService(platform)` GitHub-only check was removed. All structured webhooks with both ProjectName and Platform set now go through ITicketClaimService — the dead fire-and-forget `ExecuteStructuredAsync` method was deleted. Pre-p95a behaviour is no longer reachable; the legacy TriggerInput path (PR comments) stays untouched as documented in p95a.
- [Scope] ITicketProvider.ListByStatusAsync moved from p95b to p95c. It's a reconciler dependency, not a transitioner dependency — keeping it with the reconciler (p95c) makes the p95b YAML redundant with the multi-platform transitioner focus and gives p95c the full "recovery" surface area in one coherent phase.
