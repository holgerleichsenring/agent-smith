# Decision Log

## p0001: Core Infrastructure
- [Architecture] Command Pattern (MediatR-style) as central pattern — strict separation of "What" (context) from "How" (handler)
- [Architecture] Single generic handler interface `ICommandHandler<TContext>` instead of multiple handler types — enables DI-based dispatch
- [Tooling] YamlDotNet for configuration — chosen for simplicity, avoids overengineering (no JSON schema validation at this stage)
- [Implementation] Lazy environment variable resolution in config loader — unset vars aren't errors during loading, only at usage time
- [TradeOff] No validation of all environment variables upfront — accepted incomplete config to gain flexibility

## p0002: Pipeline Handlers
- [Architecture] CommandExecutor uses `GetService<T>()` instead of `GetRequiredService<T>()` — allows custom error messages instead of DI exceptions
- [Implementation] Handlers are sealed classes with primary constructors — enforces immutability and modern .NET 8 conventions
- [TradeOff] LoadCodingPrinciplesHandler can be fully implemented in Phase 2 but intentionally stubbed — accepted inconsistency to keep phases parallel
- [Implementation] ApprovalHandler auto-approved as stub — establishes pattern for later headless mode

## p0003: Providers
- [Architecture] Separate provider for each source control system (GitHub, Local, GitLab later) — each has different API, no shared abstraction
- [Tooling] ClaudeAgentProvider chosen as primary agent — Anthropic API, agentic loop pattern established as reference
- [Architecture] ToolExecutor separated from agent loop — allows code reuse across provider implementations
- [TradeOff] Scout phase (file discovery) designed but deferred to Phase 9 — accepted complexity later to unblock core provider work
- [Security] Path validation in tools (no `..`, no absolute paths) — prevents directory traversal attacks from agentic code execution

## p0004: Pipeline Execution
- [Architecture] Regex-based intent parser instead of LLM call — chosen for determinism, cost, and Phase 4 speed
- [Architecture] CommandContextFactory maps command names to contexts — avoids massive switch statement in PipelineExecutor
- [Implementation] ProjectConfig passed as parameter to PipelineExecutor, not injected — enables per-run configuration variation
- [TradeOff] Intent parser intentionally simple — can be swapped for Claude-based parser later without interface changes

## p0005: CLI & Docker
- [Tooling] System.CommandLine instead of manual argument parsing — provides --help, validation, version support "for free"
- [TradeOff] No sub-commands, no daemon modes, no interactive shell — "Simple: Input in → PR out → Exit"
- [Architecture] Multi-stage Docker build — separates build (SDK) from runtime (slim), targets <200MB image size
- [Implementation] Config mounted as volume, not baked into image — enables reuse without rebuild

## p0006: Retry & Resilience
- [Tooling] Polly via `Microsoft.Extensions.Http.Resilience` chosen over custom retry logic — standard, battle-tested, reduces code
- [Architecture] Retry config as typed class, not raw numbers — enables configuration without code changes
- [TradeOff] Conservative defaults (5 retries, 2sec initial, 2.0x backoff) — balances safety against cost overruns from excessive retries
- [Implementation] Exponential backoff with jitter — prevents thundering herd on rate limit recovery

## p0007: Prompt Caching
- [Architecture] `PromptCaching = AutomaticToolsAndSystem` for all API calls — caches system prompt, tool definitions, coding principles
- [Implementation] Coding Principles positioned first in system messages — maximizes cache prefix (longest stable content cached)
- [TradeOff] 5-minute ephemeral cache (API default) accepted — balance between cost savings and context freshness
- [Architecture] TokenUsageTracker separates cache metrics tracking — enables observability without coupling to loop logic

## p0008: Context Compaction
- [Architecture] Haiku for summarization, not custom rule-based compression — LLM-generated summaries more useful than keyword extraction
- [Architecture] FileReadTracker deduplicates file reads — prevents same file content from inflating conversation history
- [TradeOff] Last N iterations always preserved fully — accepted overhead to maintain recent decision context
- [Implementation] Compaction triggered periodically, not continuously — prevents overhead from constant summarization

## p0009: Model Registry & Scout
- [Architecture] TaskType enum maps task to appropriate model — Haiku for discovery, Sonnet for coding, optional Opus for reasoning
- [Architecture] Scout as implementation detail of ClaudeAgentProvider, not separate interface — keeps IAgentProvider stable
- [TradeOff] Scout is read-only (no write_file, no run_command) — accepts limitation to prevent Scout from breaking code
- [Architecture] Backward compatible: Models config is optional — existing single-model configs work unchanged

## p0010: Container Production-Ready
- [Architecture] `--headless` flag auto-approves plans — enables container/CI usage without TTY or interactive prompts
- [Implementation] Non-root user in Docker — security best practice
- [Architecture] SSH keys mounted read-only from host — enables git operations without embedding credentials

## p0011: Multi-Provider
- [Architecture] Each provider self-contained with own agentic loop — APIs too different (tool format, caching, streaming) to abstract
- [Architecture] IAgentProvider interface stable across all providers — GeneratePlanAsync, ExecutePlanAsync only methods exposed
- [Tooling] No shared "LLM abstraction layer" — accepted code duplication to gain provider-specific optimization freedom
- [TradeOff] Scout and caching Claude-only for now — other providers do direct execution, accepted feature parity gap

## p0012: Cost Tracking
- [Architecture] Prices in config, not hardcoded — providers change pricing frequently, enables user updates without code change
- [Implementation] Phase-aware token tracking (scout, planning, primary, compaction) — enables cost breakdown, not just total
- [TradeOff] Missing model pricing defaults to zero with warning — no error thrown, helps with incomplete configs

## p0013: Ticket Writeback
- [Architecture] Default interface implementations on ITicketProvider — backward compatible, existing providers work without changes
- [Implementation] Fire-and-forget ticket operations wrapped in try/catch — failures never block the pipeline
- [Architecture] CommitAndPRHandler closes ticket and posts summary — provides feedback loop to issue tracker

## p0014: GitHub Action & Webhook
- [Architecture] Both GitHub Action (zero infrastructure) and webhook listener (self-hosted) variants — choice for user deployment model
- [Tooling] HttpListener for webhook (no ASP.NET dependency) — minimal overhead for simple HTTP requirements
- [Implementation] Each webhook request spawns async Task — non-blocking, server returns 202 Accepted immediately
- [TradeOff] Trigger label configurable but defaults to "agent-smith" — simplicity over ultra-flexibility

## p0015: Azure Repos
- [Architecture] Reuses LibGit2Sharp pattern from GitHub provider — shared git operations, different PR API
- [Implementation] Clone URL embeds PAT for HTTPS auth — consistent with GitHub provider pattern
- [Architecture] PR URL constructed from org/project/repo structure — matches Azure DevOps URL format

## p0016: Jira Provider
- [Tooling] No Jira SDK — pure HttpClient with JSON, Jira REST API is straightforward
- [Implementation] ADF (Atlassian Document Format) for comments — required by Jira v3 API, wraps plain text in minimal structure
- [TradeOff] Transition names searched flexibly (contains "Done" or "Closed") — accepts incomplete workflow support over strict matching
- [Implementation] Basic Auth with email:apiToken — standard Jira Cloud authentication method

## p0017: GitLab Provider
- [Architecture] Extract `ISourceProvider` and `ITicketProvider` implementations — GitLab MRs use same semantic model as GitHub PRs, REST API v4 is straightforward JSON
- [Implementation] `LibGit2Sharp` for git operations and GitLab REST API v4 for merge requests — consistent with existing GitHub provider

## p0018: Chat Gateway
- [Architecture] Redis Streams as message bus (progress/question/done/error) with persistent consumer groups — decouples agent execution from dispatcher, enables multi-platform routing
- [Architecture] Separate `IProgressReporter` implementations: Console for CLI, Redis for K8s — single interface, pluggable output strategy
- [Implementation] K8s Job per request with ephemeral agent containers — isolation, resource limits, TTL cleanup, each fix runs independently

## p0019: K8s Deployment
- [Architecture] Multi-stage Dispatcher Dockerfile (build → runtime with non-root user) — separates compile from runtime, reduces image size, hardens security
- [TradeOff] Redis without PersistentVolume initially — ephemeral streams only live during job execution, simplifies local testing, upgrade path exists
- [Architecture] Kustomize base + dev/prod overlays — reusable structure, environment-specific patches avoid duplication

## p0019a: Docker Spawner
- [Architecture] Extract `IJobSpawner` interface with Docker and K8s implementations — single abstraction enables local docker-compose mode without K8s
- [TradeOff] Auto-detect Docker network from dispatcher container metadata — avoids explicit config in most cases, fallback to `bridge`
- [Architecture] `AutoRemove=true` on Docker containers — self-cleanup, no orphan accumulation

## p0019b: K8s Helm
- [Architecture] Helm Chart replaces manual `apply-k8s-secret.sh` — templated secrets, repeatable deployments, built-in rollback
- [TradeOff] Kustomize kept for reference — users can stick with existing workflow or migrate to Helm incrementally

## p0020a: Intent Engine
- [Architecture] Three-stage engine: Regex (free) → Haiku (cheap) → deterministic project resolution — 90% resolved free, fallback only when needed
- [Architecture] Parallel ticket provider queries for project resolution — minimizes latency on multi-project configs
- [TradeOff] Accepted regex brittleness for 90% of inputs to avoid AI cost — only calls Haiku on parse failures

## p0020b: Help Command
- [Architecture] Greeting detection in Regex stage — "hi", "hello" cost zero AI tokens
- [Implementation] `ClarificationStateManager` stores low-confidence suggestions in Redis with TTL — avoids second AI call when user confirms

## p0020c: Error UX
- [Architecture] `ErrorFormatter` with regex pattern table — maps raw errors to friendly messages, pure function fully testable
- [Architecture] `ErrorContext` carries step number + step name — users see exactly where in the pipeline failure occurred
- [TradeOff] Contact button optional via env var — graceful degradation when `OWNER_SLACK_USER_ID` not set

## p0020d: Agentic Detail Updates
- [Architecture] `ReportDetailAsync` on `IProgressReporter` — fire-and-forget so failed detail posts never abort pipeline
- [Implementation] Rate throttling on detail events (max 1 per 3 files) — prevents Slack API rate limit errors
- [TradeOff] Thread ts stored in memory only — Dispatcher restart loses thread continuity but degrades to top-level messages

## p0021: Code Quality
- [Architecture] Consistent folder structure (Contracts/, Models/, Services/) across all projects — eliminates sprawl
- [Tooling] `IHttpClientFactory` instead of `new HttpClient()` — proper lifetime management, connection pooling
- [Implementation] Magic numbers → named constants — single source of truth for configuration values

## p0022: CCS Auto-Bootstrap
- [Architecture] `ProjectDetector` is deterministic (no LLM, pure file system) — fast, zero cost, fully testable
- [Architecture] Single cheap LLM call for context generation — one-shot avoids iterative refinement, cost ~$0.01
- [TradeOff] Generated `.context.yaml` committed to repo — persistent project context, avoids regeneration on every run

## p0024: Code Map
- [Architecture] LLM-assisted extraction instead of custom parsers — hand-written AST parsing is error-prone and language-specific
- [TradeOff] Code map generation only on bootstrap or request — avoids cost on every run, requires explicit regeneration when architecture changes
- [TradeOff] 5,000 token cost per generation but saves 100k+ tokens in file reading — break-even on first agent run

## p0026: Coding Principles Detection
- [Architecture] Per-project coding principles loaded at runtime — respects project-specific style, agent sees rules before planning
- [Implementation] Principles as free-form markdown (no schema) — flexibility for humans to define culture-specific rules

## p0027: Structured Command UI
- [Architecture] Slack modals with dropdowns replace free-text parsing — no typos, structured data needs no IntentEngine
- [TradeOff] Free-text input kept as fallback — backward compatibility for power users

## p0028: .agentsmith/ Directory
- [Architecture] Unified `.agentsmith/` replaces scattered config files — single source of truth for project meta-files
- [Architecture] Run tracking as immutable records (`r{NN}-{slug}/`) — auditable changelog of all agent decisions
- [TradeOff] Failed runs recorded only in runs/ (not state.done) — state.done tracks project state changes, not execution history

## p0029: Init Project Command
- [Architecture] Generalize `IJobSpawner.SpawnAsync` from `FixTicketIntent` to generic `JobRequest` — init-project becomes first-class command

## p0030: Systemic Fixes
- [Architecture] `CommandResult` carries PR URL through pipeline — URLs were previously lost due to generic return type
- [Architecture] `OrphanJobDetector` as background service monitoring in-memory + Redis — catches orphans from pre-deployment jobs
- [Implementation] Remove `CancellationToken = default` optional parameters — forces explicit cancellation token threading

## p0031: Orphan Detector Redis Scan
- [Architecture] `ConversationStateManager.GetAllAsync()` scans `conversation:*:*` Redis keys — detects stale states from before dispatcher restart
- [Implementation] Dual-path detection: in-memory tracked + full Redis scan — comprehensive coverage of all failure modes

## p0032: Architecture Cleanup
- [Architecture] `ILlmClient` abstraction wrapping LLM providers — generators become thin prompt-builders, can swap providers without code changes
- [Architecture] `CommandNames` + `PipelinePresets` code-defined — eliminates magic strings, single source of truth
- [TradeOff] Remove language-specific collection code → feed raw files to LLM — less code, more flexible, slightly higher token cost

## p0033: Run Cost in Result
- [Architecture] YAML frontmatter in `result.md` with cost breakdown by phase — human-readable and machine-parseable, persists in repo
- [Implementation] `RunCostSummary` moved to Contracts — Application layer can access without Infrastructure dependency

## p0034: Multi-Skill Architecture
- [Architecture] Commands insert follow-up commands into flat LinkedList pipeline — no tree structures, fully transparent in logs
- [Architecture] Role-based skills with separate YAML files — pluggable roles, new roles added without code changes
- [Architecture] `ConvergenceCheckCommand` evaluates consensus — prevents infinite discussion loops
- [Safety] Max 100 command executions limit in PipelineExecutor — prevents accidental infinite loops

## p0035: Simplified Slack Commands
- [Architecture] Split `FixTicket` into three commands: `FixBug`, `FixBugNoTests`, `AddFeature` — pipeline selection implicit in command type
- [Implementation] `ModalCommandType` enum routes to pipeline presets directly — no separate pipeline dropdown

## p0036: GenerateTests & GenerateDocs + Pipeline Resilience
- [Architecture] Per-command exception handling in PipelineExecutor prevents single command crash from failing entire pipeline
- [Architecture] GenerateTests and GenerateDocs execute synthetic plans via IAgentProvider instead of custom logic — reuses agentic loop
- [Architecture] OrphanJobDetector replaced time-based detection with container liveness checking (IsAliveAsync) — eliminates false positives from slow operations

## p0037: Strategy Pattern Pipeline Abstraction
- [Architecture] Chose strategy pattern with tell-don't-ask principle — no "if type == coding" anywhere, type resolves correct implementations via DI
- [Architecture] Single class size limit of 120 lines enforced to prevent god objects and improve maintainability
- [Architecture] Renamed pipeline steps (CheckoutSource → AcquireSource, Test → Validate) despite migration burden — reflects true semantics across all pipeline types

## p0038: MAD Discussion Pipeline
- [Architecture] SkillsPath moved to ProjectConfig instead of hardcoded path — enables loading from subdirectories for different discussion types
- [Architecture] CompileDiscussionCommand writes discussion transcript instead of executing code steps — reuses existing ConvergenceCheck consolidation

## p0039: Legal Analysis Pipeline
- [TradeOff] MarkItDown preprocessing over direct PDF to Claude — accepted token overhead for searchability and consistency across document formats
- [TradeOff] Inbox polling over FileSystemWatcher — chose polling (more stable on Docker bind mounts) over event-based complexity
- [Architecture] LocalFolderSourceProvider and LocalFileOutputStrategy as Pro-only implementations — OSS receives only strategy interfaces

## p0040a: Contracts Extensions & ProviderRegistry
- [Architecture] ITypedProvider base interface for all typed providers — eliminates hardcoded factory switches via ProviderRegistry<T>
- [Architecture] AttachmentRef abstraction for all storage sources — supports multiple backends without coupling to specific implementation
- [Implementation] Config passed at construction time (via IOptions<T>) not at factory method call — cleaner DI composition

## p0041: Decision Log
- [Architecture] IDecisionLogger with nullable repoPath and tell-don't-ask pattern — handlers always log, implementation decides what to do
- [Implementation] FileDecisionLogger + InMemoryDecisionLogger split — coding pipelines write to file, non-repo pipelines skip silently
- [Architecture] Decisions captured at decision moment (GeneratePlan, AgenticExecute, Bootstrap) not post-processed — ensures accuracy

## p0042: Legal Analysis Pipeline Handlers
- [TradeOff] InboxPollingService uses copy-then-delete not move — idempotent on crash, file remains in inbox for retry on next poll
- [Implementation] Contract type detected via Scout (cheap Haiku call) to select correct legal skill subset

## p0043a: CI/CD Docker Publish
- [Tooling] Chosen multi-arch build (amd64 + arm64) with QEMU emulation — supports both x86 servers and Apple Silicon developers
- [TradeOff] Stayed on mcr.microsoft.com/dotnet/sdk:8.0 for Host despite larger image — needed for cloned repo build/test execution

## p0043b: Security Pipeline
- [Architecture] Chosen IPrDiffProvider interface over PR-specific methods — enables diff-only scan without full checkout
- [Tooling] SecurityScan pipeline preset reuses Triage + ConvergenceCheck from discussion pattern — no duplicated state management
- [Architecture] DeliverOutputCommand with IOutputStrategy keyed services (.NET 8) — pluggable output backends without factory methods

## p0043c: SARIF Output Strategy
- [Architecture] IPrCommentProvider injected separately into SarifOutputStrategy — decouples platform-specific comment posting from finding analysis
- [Implementation] Skill output requires structured format (file/line/severity/confidence) instead of prose — enables SARIF mapping without LLM re-parsing

## p0043d: Ollama Local Model Support
- [Tooling] OpenAiCompatibleClient shared between OpenAI and Ollama via composition not inheritance — avoids class hierarchy complexity
- [TradeOff] Structured text fallback when tool calling unavailable — preferred over forcing minimum model capability
- [Implementation] ModelAssignment extended with ProviderType and Endpoint (both nullable) — backward compatible with existing single-provider configs
- [Architecture] Startup validation pings Ollama endpoint and detects tool calling capability — fails fast with clear error messages

## p0043e: Webhook Expansion
- [Architecture] Replaced monolithic WebhookListener with IWebhookHandler dispatch pattern — eliminates large switch statement, enables platform handlers in isolation
- [Architecture] SecurityScanRequest as common mapping target across all platforms — decouples platform-specific payloads from execution logic

## p0044: Rename ProcessTicket → ExecutePipeline
- [Architecture] Renamed ProcessTicketUseCase to ExecutePipelineUseCase — reflects that system orchestrates arbitrary workflows, not just tickets

## p0045: API Security Scan Pipeline
- [Architecture] Reused TriageHandlerBase and SkillRoundHandlerBase from p0043b instead of duplicating — established pattern for cascading commands
- [Tooling] Chose Nuclei over manual HTTP scanning — handles authentication, redirects, and HTTP semantics that would require custom crawling

## p0046: CLI Refactor & LLM Intent Parsing
- [Architecture] CLI changed to explicit flags (agent-smith fix --ticket 42) instead of free-text parsing — self-documenting and extensible
- [Tooling] Replaced regex-based intent parsing with Haiku LLM parsing for Slack — handles natural language variations, eliminates pattern fragility
- [Architecture] Collapsed ExecuteTicketAsync/ExecuteTicketlessAsync/ExecuteInitAsync into ExecuteAsync(PipelineRequest) — single code path regardless of source
- [Implementation] Program.cs split from 456 to <30 lines — each command handler under 120 lines, extracted Banner and ServiceProviderFactory

## p0047: API Contract & Schema Analysis
- [Architecture] Spectral runs as second static analyzer alongside Nuclei — complements mechanical scan with structural validation
- [Tooling] api-design-auditor rewritten with full schema analysis — semantic layer that tools cannot reach (sensitive data bundling, enum opacity, REST semantics)
- [Implementation] Spectral findings feed same skill pipeline as Nuclei — unified analysis despite different scanner types

## p0048: Swagger Context Compression
- [Architecture] Compression happens in handler (ApiSkillRoundHandler.BuildDomainSection) not as pipeline command — formatting concern, not workflow step
- [TradeOff] Deduplication + schema reference strategy over raw swagger.json — accepted 85% token reduction over complete fidelity

## p0049: Tool Runner Abstraction
- [Architecture] IToolRunner abstraction with multiple implementations (Docker/Podman/K8s/Process) — single interface handles all deployment modes
- [TradeOff] Spawners hardcode container paths (currently hardcoded, improved in p0051) — simplifies path handling despite DockerToolRunner complexity

## p0050: Multi-Output Strategy
- [Architecture] --output accepts comma-separated values, multiple IOutputStrategy implementations run once — avoids duplication but enables customized output
- [Implementation] SummaryOutputStrategy for clean findings view — filters skill discussion noise, shows only retained findings grouped by severity
- [Implementation] OutputDir resolved once in handler not per-strategy — eliminates per-strategy fallback chains

## p0051: ToolRunner Clean Architecture
- [Architecture] ToolRunRequest uses {input}/{output} placeholders resolved per-runner — logical I/O decoupled from execution model paths
- [Implementation] Each runner translates placeholders to its model (/work for Docker, tempdir for Process, /work for K8s) — clean separation of concerns
- [TradeOff] No trimming for single executable — reflection-heavy dependencies (YamlDotNet, Docker.DotNet) make trimming unstable

## p0052: Single Executable Release
- [Tooling] PublishSingleFile + SelfContained without trimming — ~70-80MB binaries acceptable, avoids reflection breakage
- [Architecture] Config file discovery chain (.agentsmith/, ./config/, ~/.agentsmith/) — no hardcoded paths, works across deployment contexts
- [Implementation] Docker entrypoint script fixes mount permissions and drops to agentsmith user — eliminates permission issues without requiring consumer setup

## p0053: Documentation Site
- [Architecture] MkDocs + Material theme over custom solution — reduces maintenance burden, leverages community defaults
- [Architecture] README trimmed to landing page, docs site as authoritative source — solves discoverability and information architecture split
- [Architecture] GitHub Pages deployment over managed hosting — free, version-controlled docs that stay with the code

## p0054: Security Scan Expansion
- [Architecture] Deterministic tools (regex, git history, dependency auditing) precede LLM skills — improves recall on pattern-based findings, reduces token cost while maintaining context awareness
- [Tooling] YAML pattern files are user-extensible without code changes — enables users to add project-specific patterns without rebuilding
- [Tooling] Separate tool commands (StaticPatternScan, GitHistoryScan, DependencyAudit) over monolithic "scanner" — each can be skipped independently based on repo type
- [TradeOff] Pattern-based pre-scanning accepted higher false positives over perfect recall — LLM specialists later filter and contextualize

## p0055: Security Findings Compression
- [Implementation] Top-N detail + remainder summary per category — reduces token cost 70%+ while keeping findings accessible to relevant skills
- [Architecture] Category-based slicing to skills instead of full findings dump — each skill only receives data it can act on
- [TradeOff] Compressed threshold (≤15 findings = full detail) over uniform compression — small categories skip compression to preserve context

## p0056: Security Scan Polish
- [Implementation] Mandatory false-positive-filter always runs regardless of triage — ensures consistency, prevents missed filtering
- [Architecture] ExtractFindingsCommand unifies security-scan and api-scan delivery paths via SARIF — consistent output format across pipelines
- [Tooling] SecretProviderRegistry maps patterns to revoke URLs without live API probing — actionable for developers without runtime risk

## p0057a: Skill Standard
- [Architecture] Three-file skill structure (SKILL.md + agentsmith.md + source.md) over single YAML — separates portable skill content from Agent-Smith extensions and provenance
- [TradeOff] Local skills require source.md with origin=local — enforces explicit knowledge of whether skills are internal or external

## p0057b: Skill Manager Pipeline
- [Implementation] Human approval mandatory before skill installation — treats skills as code injection, no automated installation
- [Architecture] SKILL.md never modified, extensions in agentsmith.md only — preserves upstream skill integrity, enables future updates
- [TradeOff] Pinned versions in source.md over automatic updates — requires explicit review cycle for new skill versions

## p0057c: Autonomous Pipeline
- [Architecture] Agent writes tickets, human decides whether to act — inverts control: agent prioritizes, human validates rather than human tasking agent
- [Implementation] Three-part ranking from convergence (not exhaustive list) — avoids ticket spam, forces prioritization through multi-skill agreement
- [TradeOff] project-vision.md must exist before autonomous runs — human knowledge about what matters cannot be inferred from code alone

## p0058: Interactive Dialogue
- [Architecture] Typed DialogQuestion (Confirmation, Choice, FreeText, Approval, Info) over binary Yes/No — supports nuanced human input without proliferating new adapters
- [Implementation] Dialogue trail always in result.md, never lost — audit trail of all agent-human interaction tied to the run
- [TradeOff] Timeout defaults to sensible value rather than infinite wait — CLI/PR workflows don't block forever without human
- [Implementation] Agent has ask_human tool with system prompt rules about when to ask — prevents overuse while allowing genuine clarifications

## p0058b: Microsoft Teams Integration
- [Implementation] Adaptive Cards for all 5 QuestionType variants — Teams-native UI that preserves same dialogue model across all adapters
- [TradeOff] Removed old AskQuestion method entirely once all adapters migrated — cleaner contract, forces consistency

## p0059: PR Comment as Input
- [Architecture] Unified webhook dispatch pattern (IWebhookHandler) handles both GitHub Issues and PR Comments — one entry point scales to multiple event types
- [Implementation] CommentIntentRouter distinguishes new job vs. dialogue answer at parse time — same Redis infrastructure serves both scenarios
- [Architecture] Platform-agnostic CommentIntent model, platform-specific webhook handlers — enables p0059b/p0059c without changing routing logic
- [TradeOff] Sparse checkout + no container job for PR context vs. full agent container — trade implementation simplicity for PR-specific context loading

## p0059b: GitLab MR Comments
- [Implementation] GitLab webhook handler added, CommentIntentRouter reused — confirms separation of platform-specific handling from intent logic

## p0059c: Azure DevOps PR Comments
- [Implementation] AzDO webhook handler added, CommentIntentRouter reused — demonstrates platform extension pattern scales

## p0060: Security Pipeline Enhancements
- [Architecture] DAST via ZAP after static tools, not integrated into static — runtime testing reveals issues invisible in code, separate tool appropriate
- [Implementation] Auto-fix spawns separate K8s jobs, doesn't block security scan — security findings delivered immediately, fixes continue asynchronously
- [TradeOff] Git-based trend analysis over external database — leverages YAML frontmatter + git history, no persistence layer
- [Implementation] Trend committed to default branch, not PR branch — accumulated knowledge stays project-wide, avoids merge conflicts

## p0061: Project Knowledge Base
- [Architecture] wiki/ compiled incrementally by LLM from runs/ + security/ — Karpathy pattern: no embeddings, no vectors, human-readable markdown
- [Implementation] CompileKnowledge respects rate limiting (default 1x/hour) — prevents token waste on frequent small runs
- [Architecture] QueryIntent requires @agent-smith prefix — prevents collision with ticket descriptions that start with questions
- [Implementation] Wiki queries use sparse checkout of .agentsmith/wiki/ only — 5-15 seconds instead of minutes, no container job needed
- [TradeOff] Decisions compiled from multiple runs, not just latest — institutional memory accumulates; contradictions detected by WikiLint

## p0062: Docs Site Update
- [Implementation] Docs updated after p0058–p0061 complete — ensures documentation describes implemented features, not planned ones

## p0063: Structured Finding Assessment
- [Implementation] All findings reach skills (remove Top-N cap), compression changes formatting not visibility — every finding gets at least one-line assessment
- [Architecture] Finding record gains ReviewStatus (not_reviewed | confirmed | false_positive) — tracks agent assessment, enables filtering
- [Implementation] Convergence produces JSON assessments alongside prose — structured output that actually flows into DeliverFindings
- [TradeOff] Accepted ~3-4k token increase per skill call vs. hidden findings that skills never see — visibility trumps compression

## p0064: Typed Skill Orchestration
- [Architecture] Pipeline type declared (discussion | structured | hierarchical) → determines orchestration model — one model doesn't fit all
- [Implementation] SkillGraphBuilder replaces LLM-based Triage for structured/hierarchical — deterministic execution graph from skill metadata, no LLM call needed
- [Architecture] Contributors receive only their input_categories slice, run in parallel independently — 80% token reduction vs. free-form discussion
- [Implementation] Gate role produces structured JSON (confirmed | rejected) instead of free-text discussion — enables filtering to actually affect output
- [TradeOff] Fallback to discussion model (contributor role) for skills without orchestration block — backward compatible, new pipelines get defaults

## p0065: Website Redesign
- [Architecture] CSS-only redesign, no template changes — isolates visual changes from site structure
- [Implementation] Shadow-as-border (1px rgba border) over CSS borders — aligns with Vercel/Geist aesthetic while maintaining simple HTML
- [TradeOff] Three font weights only (400/500/600) over full spectrum — constrains design but reduces font loading, enforces visual hierarchy

## p0066: Docs Enhancement — Self-Documentation & Multi-Agent Orchestration
- [Architecture] DESIGN.md placed in docs/ not project root — it is a docs-site concern, not product code
- [Tooling] CSS-only theme overrides via extra_css, no custom MkDocs templates — keeps MkDocs upgrades safe
- [TradeOff] Content first, styling second — missing content is a blocker, imperfect styling is not
- [Implementation] Reuse existing fix-and-feature.md instead of creating separate fix-bug.md — page already covers both pipelines

## p0067: API Scan Compression & ZAP Fix
- [Architecture] Category slicing (auth/design/runtime) instead of finding compression — findings are already compact at ~90 chars/piece, compression would lose information. Slicing routes findings to the right skill without data loss.
- [Tooling] WorkDir as optional ToolRunRequest parameter instead of Docker volume mounts — volume mounts would add complexity to DockerToolRunner. WorkDir + tar extraction to / is simpler and backward compatible (Nuclei/Spectral unaffected).
- [Implementation] Inject target URL into swagger servers[] instead of pinning ZAP version — ZAP needs absolute URLs, many OpenAPI specs only have relative "/". Patching the spec before copy is non-invasive.
- [TradeOff] Remove --auto flag entirely instead of finding replacement — --auto was never a valid option on ZAP's Python wrapper scripts. The scripts are non-interactive by default in Docker containers.
- [Implementation] Skip DAST skills on ZAP failure via ZapFailed flag — avoids wasting 2 LLM calls on empty input. Flag is checked in ApiSecurityTriageHandler before building the skill graph.

## p0068: API Finding Location
- [Architecture] DisplayLocation as computed property on Finding record — no new field in serialization, just display logic. Fallback chain: ApiPath > SchemaName > File:StartLine.
- [TradeOff] Nullable fields instead of separate ApiFinding subtype — keeps one Finding type across all pipelines. Security-scan findings simply leave ApiPath/SchemaName null.
- [Implementation] NullIfEmpty normalization in ParseGateFindings — LLMs return empty strings instead of omitting fields. Normalize at parse time and defend in DisplayLocation with IsNullOrWhiteSpace.

## p0070: Decision Log — Phase/Run Context
- [Architecture] sourceLabel as section header instead of category — the real lookup key is "which phase made this decision", not "what kind of decision was it". Category preserved as inline tag.
- [TradeOff] Optional parameter instead of new overload — keeps backward compat, callers that don't know their phase context still work with category-only sections.
- [Implementation] Ticket ID as sourceLabel in pipeline handlers — GeneratePlanHandler and AgenticExecuteHandler pass #{ticketId} so run decisions land under their ticket.

## p0072: Jira Assignee Webhook Trigger
- [Architecture] Handler returns WebhookResult(TriggerInput, Pipeline) instead of enqueueing jobs directly — follows existing dispatch pattern where WebhookListener calls ExecutePipelineUseCase. Phase doc proposed IJobEnqueuer; actual codebase delegates execution to the Listener.
- [Architecture] ServerContext record for config path DI — handler needs config for assignee matching and label→pipeline resolution, but IWebhookHandler.HandleAsync has no configPath parameter. Introduced ServerContext(ConfigPath) registered at server startup, injected into handler. Minimal surface, no interface changes.
- [Security] Secret configured + signature header missing → reject — phase doc originally returned true (skip), which would let attackers bypass verification by omitting the header. Fixed to return false.
- [Implementation] Config-order priority for label→pipeline resolution — iterate PipelineFromLabel keys (user-defined order) instead of payload labels (Jira-side order is undocumented and non-deterministic). Gives users explicit control over priority.
- [Implementation] WebhookListener extracts webhookEvent from Jira payload, strips "jira:" prefix → eventType for CanHandle — Jira has no event-type header unlike GitHub/GitLab. Listener parses once before dispatch.
- [TradeOff] Jira signature validation loads config per request instead of caching — keeps validation consistent with runtime config changes. Acceptable cost for webhook traffic volume.
- [Scope] Unassign scenario (Agent Smith removed from ticket) explicitly out of scope — handler returns Ignored correctly, but cancelling a running job requires Job Cancellation feature that doesn't exist yet.

## p0077: Pipeline Fixes
- [Architecture] skills_path resolved relative to config file directory — same pattern as tsconfig.json, docker-compose.yml. Config file is the anchor point, not CWD or repo root. Covers: local scan (repo has its own skills), API scan (no repo), CI with separate config repo (RHS.CICD/tools/agent-smith/config/).
- [Fix] ZAP exit codes 0-3 are valid scan results (pass/info/warnings/failures), not errors — only >3 indicates a tool crash. Previously any non-zero was treated as failure, discarding valid DAST findings.
- [Fix] Docker cp directories created with mode 0777 — ZAP container runs as UID 1000 (zap user), but docker cp creates files as root. World-writable permissions fix the PermissionDenied on /zap/wrk/zap-report.json.

## p0073: Class Size Enforcement
- [Quality] CI gate starts non-blocking, flip to blocking after Tier 1+2 — allows incremental adoption without breaking builds.
- [Scope] Pure refactoring only (extract, rename, split — no behavior changes) — ensures no regressions from size enforcement work.

## p0074: CLI Source Overrides
- [Architecture] Three generic options (--source-type, --source-path, --source-url, --source-auth) replace --repo — covers all provider types uniformly.
- [Architecture] Applied in ExecutePipelineUseCase (transparent to downstream) — handlers don't need to know about CLI overrides.
- [Cleanup] SecurityScanCommand drops --repo — replaced by generic source options.

## p0075: Phase Docs to YAML
- [Architecture] YAML over markdown — structured input yields structured output, ~10x token reduction (96k → 19k words).
- [Scope] No code examples in phase specs — agent reads codebase directly, examples become stale.
- [Scope] Type signatures kept, action field supports single value or list.
- [Scope] Delete original markdown files — git history is the archive.

## p0076: Azure OpenAI Provider
- [Architecture] Inheritance over composition — only CreateChatClient differs from OpenAiAgentProvider.
- [Configuration] Only 'deployment' and 'api_version' added to AgentConfig — api_version defaults to 2025-01-01-preview.
- [Tooling] Azure.AI.OpenAI NuGet (official Microsoft package).

## p0078: Gate Category Routing
- [Architecture] Filter before gate, merge after — each gate sees only its input_categories, results are combined.
- [Architecture] Unmatched findings pass through unchanged — categories not claimed by any gate are not lost.
- [Architecture] No gate ordering dependency — gates in same stage run independently on disjoint slices.

## p0081: Integration Setup Docs
- [Scope] All guides in English, under docs/setup/ as dedicated section.
- [Scope] Slack guide review/update, Teams guide is new (Azure Bot registration, ngrok, manifest, docker-compose env vars).
- [Scope] Webhook and label-trigger docs split to p0082.
- [Decision] Manifest template under docs/setup/teams/ (not deploy/).
- [Decision] Teams status marked 'beta' in chat-gateway.md.
- [Decision] No placeholder PNGs — document icon requirements in text instead.

## p0083: Jira Webhook Status Lifecycle
- [Architecture] Trigger requires all three: assignee match + label match + status in whitelist — prevents accidental runs on resolved/closed tickets.
- [Architecture] Comment trigger (comment_created) is a separate handler, not a mode in JiraAssigneeWebhookHandler — separate event type, cleaner routing.
- [Architecture] Comment trigger also checks status whitelist — prevents triggering on closed tickets.
- [Architecture] done_status resolved via transition name, not status ID — names are human-readable and portable across Jira projects.
- [Architecture] All pipelines are valid trigger targets — pipeline_from_label maps to any pipeline name, no hardcoded restrictions.
- [Reuse] Status transition after PR extracts TransitionToAsync from JiraTicketProvider.CloseTicketAsync — reusable for any target status.
- [Flow] DoneStatus flows via WebhookResult.InitialContext → PipelineContext → CommitAndPRHandler — no coupling between webhook handler and pipeline handler.
- [Note] PipelineFromLabel uses Dictionary<string,string> — insertion order preserved by YamlDotNet but not spec-guaranteed. Migrate to OrderedDictionary<TKey,TValue> when moving to .NET 9.

## p0082: Webhook & Trigger Docs
- [Scope] Per-platform setup guides under docs/setup/webhooks/ — each with prerequisites, step-by-step, config YAML, verification, troubleshooting.
- [Scope] Label-triggers overview documents current state per platform and p0084 roadmap for unified configuration.
- [Decision] Guides link to each other and to configuration reference (webhooks.md) — no duplication of config details.

## p0085: Webhook Structured Dispatch
- [Architecture] Webhook handlers return ProjectName + TicketId instead of free-text TriggerInput — WebhookRequestProcessor builds PipelineRequest directly, bypassing RegexIntentParser.
- [Architecture] PR comment handlers still use TriggerInput string (free-form arguments) — legacy path preserved as fallback.
- [Fix] ConsolidatedPlan is input for GeneratePlanHandler, not a replacement — multi-skill discussion provides analysis context, GeneratePlanHandler distills concrete PlanSteps. Previously the handler short-circuited with 0 steps, causing the agent to spend 32 iterations guessing what to do.
- [Fix] Jira Cloud system webhooks don't send signature headers — signature validation now skips when no header present, even if secret is configured.

## p0084: Unified Webhook Lifecycle
- [Architecture] WebhookTriggerConfig as shared base for all platforms — status gate, pipeline-from-label, done_status, comment re-trigger unified across GitHub, GitLab, Azure DevOps, Jira.
- [Architecture] Hardcoded trigger labels/tags replaced by config with backward-compatible defaults — existing setups work without changes.
- [Architecture] All platforms support pipeline_from_label — any pipeline is a valid trigger target, no hardcoded restrictions.

## p0086: Typed Skill Observations
- [Architecture] SkillObservation as universal output contract — replaces free-text DiscussionLog, pipeline-agnostic structured data.
- [Architecture] ID assigned by framework, not LLM — prevents hallucinated or colliding IDs.
- [Architecture] ConvergenceResult replaces ConsolidatedPlan string — mechanical plan generation from structured input.
- [Architecture] GeneratePlanHandler always runs — ConsolidatedPlan is context for plan generation, not a replacement.
- [Implementation] Concern, Severity, Effort as enums with JsonStringEnumConverter — type-safe, serialization-friendly.
- [TradeOff] Partial valid JSON accepted — take valid observations, skip broken ones with warning. Robustness over strictness.

## p0079/p0080: Attacker-Perspective Security Skills
- [Architecture] Commodity tools (StaticPatternScan, GitHistoryScan, DependencyAudit) are passive layer — unchanged. Intelligence layer (LLM attacker skills) is active layer on top.
- [Architecture] Attacker-perspective skills (recon-analyst, low-privilege-attacker, idor-prober, input-abuser, response-analyst) complement knowledge-domain skills — both run together.
- [Architecture] chain-analyst is executor — receives all commodity + skill findings and produces chained attack assessment.
- [Scope] No runtime, no user accounts, no HTTP probing — code/diff analysis only.

## p0087: Ticket Image Attachments
- [Architecture] IAttachmentLoader per platform — keeps TicketProvider focused on ticket CRUD, image downloading is separate concern.
- [Implementation] Only image MIME types (png, jpeg, gif, webp) — skip PDFs, ZIPs. Max 5MB per image, skip larger with warning.
- [Implementation] Images stored as base64 in PipelineContext via ContextKeys.Attachments — passed as vision input to LLM during plan generation.
- [TradeOff] Markdown parsing for GitHub/GitLab (![](url) patterns) over API-based attachment listing — issues embed images inline, not as separate attachments.

## p0088: Configurable Defaults
- [Architecture] Config wins over hardcoded — every value that depends on the user's environment must be readable from agentsmith.yml.
- [Architecture] Ticket states as whitelist (IN clause) instead of blacklist (<> exclusions) — unknown states excluded by default. Blacklists in foreign systems are a bug machine.
- [Architecture] PR target branch: repo API → config → "main" fallback chain. API result cached per run — one extra API call, not per PR.
- [Architecture] Missing ADO fields map to null, not errors — AcceptanceCriteria may not exist in all process templates.
- [Architecture] GitLab base URL required for self-hosted — no silent fallback to gitlab.com. Startup validation error with clear message.
- [TradeOff] ADO API version via env var (AZDO_API_VERSION) instead of config file — env vars are standard for server-side overrides, config file would be over-engineering for a rarely changed value.

## p0089a: Skill Content Improvements
- [Architecture] Confidence calibration defined once in observation-schema.md — Low (0-30), Medium (31-69), High (70-100). Referenced by all skills.
- [Architecture] Framework-specific false-positive rules derived from Anthropic's claude-code-security-review — 12 precedents battle-tested across thousands of reviews.
- [Architecture] Phase 1 repo context exploration before analysis — skills must understand existing security patterns before flagging findings. Deviations from established patterns are more likely real findings.
- [Scope] Content only — zero C# changes, zero build risk.

## p0092: K8s Config Cleanup
- [Architecture] Flat numbered YAMLs over Kustomize — GitOps repos (ArgoCD) apply plain YAML directly; Kustomize/Helm can be derived from flat files if needed, not the other way around
- [Architecture] Dev/prod differences as inline comments (e.g. `# prod: 2`) instead of overlay patches — this is a reference deployment, not a production GitOps repo
- [Implementation] Shell script over Makefile for ConfigMap regeneration — project has no Makefile, standalone script in deploy/k8s/ is more discoverable
- [Implementation] Two placeholder projects (GitLab+Claude, AzureDevOps+AzureOpenAI) — covers provider combos not already shown in agentsmith.example.yml (which defaults to GitHub+Claude)
- [Implementation] Pricing moved under agent block in example.yml — matches current real config structure where pricing is provider-specific, not project-level
- [Scope] Config/infra only — zero C# changes, zero build risk

## p0093: LLM Output Error Handling
- [Architecture] Silent catch banned for LLM-output parsing — every parse failure logs the response and returns Fail. Silent pass hid unreliable gates; a scan could report "647 raw → 647 extracted" while the gate had actually failed to parse.
- [Architecture] One corrective retry in the gate path (`GateRetryCoordinator`) — cheap (one extra LLM call worst case), recovers most schema drift. Retry failure fails the pipeline rather than silently letting findings through, because an unfiltered scan masquerading as filtered is worse than an explicit failure.
- [Architecture] Gate (output: list) must declare `input_categories` explicitly — `*` for wildcard or a concrete list. Empty/missing is rejected at skill load. Gate (output: verdict) is exempt because it doesn't filter findings.
- [Architecture] Validation throws at load time, skill-loader logs as Error — invalid skills don't silently vanish from the role set; the error is visible before the pipeline runs.
- [Scope] LLM-output parsing only — infrastructure cleanup catches (Docker, Process, file fallbacks) are legitimately best-effort and stayed unchanged. Non-gate parsers (Consolidation, Wiki) got logging on their existing fallbacks, not behavioral changes, because their fallback paths are semantically valid (degraded text, empty dict).

## p0094a: Gitignore-Aware Source Enumeration
- [Architecture] LibGit2Sharp's `Repository.Ignore.IsPathIgnored` is the ignore source of truth when scanning a git repo — nested `.gitignore`, `.git/info/exclude`, and global excludes all covered without a hand-rolled gitignore parser.
- [Architecture] Hardcoded `ExcludedDirectories` list shrunk but kept as a non-git fallback — `.git/`, `node_modules/`, `bin/`, `obj/`, `__pycache__/`, `.vs/`, `.idea/` stay for CLI scans of arbitrary local paths; `dist/`, `build/`, `vendor/`, `packages/` removed because real repos gitignore them anyway.
- [Architecture] Binary-extension filter stays orthogonal to gitignore — `.gitignore` doesn't mark `.png` as binary, and many repos correctly track binary assets. Two independent reasons to skip a file.
- [Implementation] macOS `/var` → `/private/var` symlink fallback in `GitIgnoreResolver.ToRelative` — realpath canonicalization inside LibGit2Sharp vs. caller-supplied `/var/...` paths breaks direct `Path.GetRelativePath`; a targeted `/private/` prefix strip handles the common test/scratch case without introducing a P/Invoke dependency.
- [Scope] Enumeration only — no changes to pattern definitions, scanner, or findings model. Observable effect limited to fewer files reaching the scanner for repos that gitignore their build output.

## p0094b: Security-Scan Skill Reduction (15 → 9)
- [Architecture] Attacker-perspective skills (vuln-analyst, recon-analyst, response-analyst, low-privilege-attacker, input-abuser, idor-prober) deleted from `config/skills/security/` — in a code-audit context without HTTP probing, their prompt output overlapped with the knowledge-domain skills (auth-reviewer, injection-checker, config-auditor, compliance-checker). The same skills remain in `config/skills/api-security/` where HTTP probing and persona-based testing give them genuinely distinct capabilities.
- [Architecture] idor-prober replaced by two independent mechanisms: (1) auth-reviewer's SKILL.md scope extended to explicitly cover IDOR/BOLA (sequential IDs, ownership predicates, cross-tenant, bulk-ops); (2) new `config/patterns/auth.yaml` with 4 static patterns (ASP.NET int route constraint, EF Find-by-id, LINQ single-predicate ID lookup, raw SQL where-id-only). LLM covers nuance, static patterns catch deterministic signals cheaply.
- [Architecture] Legacy `SkillCategories` dictionary in `SecurityFindingsCompressor` removed — since p0093 made `orchestration.InputCategories` authoritative, the fallback branch was unread code for every skill with proper input_categories. With the dict gone, an undeclared Contributor skill now returns an empty slice, which is the correct behaviour (gets the overall findings summary instead).
- [Architecture] `GetSliceForSkill` gained explicit wildcard handling — `input_categories: ["*"]` now concatenates every category slice, matching the documented p0093 semantics. Before this change the gate reached for `categorySlices["*"]` (never present) and silently returned empty; the overall summary was a hidden fallback that masked the bug.
- [Scope] Baseline and comparison numbers recorded in the phase spec as decision keys: 15-skill baseline wallclock=4m15s, post-gate=14 findings; 9-skill run wallclock=3m (29% faster, target ≥25% met), post-gate=10. Severity-weighted variance is -18% (outside the ±15% done criterion), attributable to LLM gate-judgement variance on pattern-definition files rather than real coverage loss — both runs mostly filter `config/patterns/*.yaml` regex samples, and each run keeps a different subset. Follow-up candidate: exclude `config/patterns/*.yaml` from self-scans.
- [TradeOff] ±15% severity-weighted tolerance was documented as a hard done criterion but proved tighter than single-run LLM determinism supports. Rather than re-run until we hit the target, the variance is recorded honestly with root-cause analysis; future coverage criteria should either average over multiple runs or set a wider tolerance.

## p0095a: Ticket-Claim Spine (GitHub only)
- [Architecture] Split the original p0095 "ticket-claim-lifecycle" phase into p0095a (this), p0095b (all-platform transitioners), and p0095c (heartbeat + reconciler + full config). Reason: the original p0095 bundled ~27 new prod files + 4 heterogeneous platform API integrations + hosted services + config schema into one phase — too large to commit incrementally. Splitting gives each sub-phase a testable, committable increment. Documented in the three phase YAMLs; p0096 (poller) now `requires: [p0095a, p0095b, p0095c]`.
- [Architecture] Introduced IRedisJobQueue (RPUSH/LPOP FIFO on `agentsmith:queue:jobs`) as the durable handoff between webhook receiver and worker. Webhook no longer runs pipelines fire-and-forget in-process. Before p0095a this path didn't exist — the existing ServerMode invoked `ExecutePipelineUseCase` directly from the webhook handler, which had no backpressure and no crash recovery. The queue is ephemeral (ticket status is the truth); recovery lands in p0095c.
- [Architecture] IRedisClaimLock uses SETNX for acquisition and a CAS Lua script for release (returns the acquirer token; release only deletes if the stored value matches). Prevents a caller whose lock TTL expired from deleting a lock subsequently re-acquired by another process. Simple Redis DEL without CAS was considered and rejected — the failure mode it enables (releasing a foreign lock during clock skew or GC pause) is subtle enough to warrant the Lua.
- [Implementation] ConsumeAsync polls via LPOP + 1s delay instead of BRPOP. StackExchange.Redis's IDatabase doesn't expose BRPOP directly; raw ExecuteAsync would work but adds complexity. Polling matches the pattern already used by RedisMessageBus and keeps the queue code under 80 lines. Latency impact: ~500ms worst case on queue entry → visible to consumer, acceptable given pipeline wall time dominates.
- [Architecture] PipelineQueueConsumer is a plain class with RunAsync(CancellationToken), not a `BackgroundService`. The Cli `server` command isn't built on `IHost` (it uses a direct ServiceProvider), so BackgroundService would require introducing IHost just for this consumer. Matches WebhookListener's pattern; both are awaited via Task.WhenAll inside ServerCommand. When/if Cli migrates to IHost in the future, this becomes a BackgroundService with one line of DI.
- [Architecture] QueueConfig lives on the root AgentSmithConfig (not per-project ProjectConfig.Agent), because the queue key is process-wide and max_parallel_jobs caps pipelines across all projects on one pod. Per-project tuning would give wrong semantics (a busy project would unfairly hog slots).
- [Architecture] Platform-specific routing of webhooks-to-ClaimService is a string check `result.Platform == "GitHub"` in WebhookRequestProcessor. For p0095a, GitLab/AzDO/Jira keep their in-process ExecuteStructuredAsync fire-and-forget path. p0095b will flip them over once their transitioners exist. Documented explicitly in the WebhookRequestProcessor class summary so the legacy branch isn't mistaken for dead code.
- [TradeOff] PipelineRequest moved from AgentSmith.Application/Models to AgentSmith.Contracts/Models. The queue interface lives in Contracts (infrastructure implements; application consumes), and accepting PipelineRequest forces it into the Contracts layer. Touched ~14 files' `using` directives — small one-time churn, correct layering.
- [Architecture] GitHub transitioner uses If-Match ETag on PATCH /issues/{n} with a full labels-array replacement. GitHub's actual ETag enforcement on issue PATCH is documented but not guaranteed to 412 consistently in practice; the code handles 412 correctly if it comes, otherwise accepts last-write-wins semantics. p0095c's full lifecycle config + reconciler limits the blast radius of a lost transition.
- [Scope] ITicketClaimService interface takes AgentSmithConfig as a method parameter (not via DI). The caller (WebhookRequestProcessor) already loads config each request for other reasons; forcing the service to load it adds ConfigPathHolder plumbing without benefit. Keeps the service stateless and easy to unit-test.
- [Scope] PR-comment webhook path (TriggerInput, dialogue answers) is explicitly untouched in p0095a. Spec calls it out. Those flows don't own ticket lifecycle and don't benefit from claim semantics. Migrating them is a separate concern, not a sub-phase of p0095.

## p0095b: Multi-Platform Status Transitioners
- [Architecture] GitLab uses PUT /issues/{iid} with add_labels/remove_labels rather than full-labels replacement. Targets only the lifecycle label, leaves unrelated labels untouched. No ETag on GitLab issues — concurrency is last-write-wins at the platform level; TicketClaimService's SETNX claim-lock carries the primary race guard. Full-label PUT was considered and rejected — concurrent edits to non-lifecycle labels would be clobbered.
- [Architecture] AzureDevOps uses JSON Patch on /workitems/{id} with an explicit `test /rev` operation before the `add /fields/System.Tags` — AzDO returns 412 (PreconditionFailed) on rev mismatch. System.Tags is a semicolon-separated list; we read-filter-add-serialize. 409 Conflict treated as PreconditionFailed too (AzDO occasionally returns that for concurrent modifications).
- [Architecture] Jira label-mode uses an *additional* SETNX label-lock (`agentsmith:jira-label-lock:{ticketId}`, 10s TTL) on top of the global claim-lock that TicketClaimService already holds. Reason: Jira's PUT fields.labels is not atomic — two concurrent PUTs from the same pod could both succeed and clobber each other within the claim-lock's validity window if the window straddled a GET/PUT pair. The label-lock narrows the atomic critical section specifically to the label mutation. p0095a's single-lock pattern was considered and rejected for Jira specifically.
- [Architecture] JiraWorkflowCatalog ships in p0095b as a skeleton — always returns Label-mode regardless of project. Native probing (GET /rest/api/3/project/{key}/statuses) waits for p0095c when LifecycleConfig introduces per-project status name customisation; probing before that would force native-status mapping into the p0095b scope. Structure is in place (ConcurrentDictionary cache, `SetModeForTest` internals) so p0095c only flips the probe logic.
- [Scope] WebhookRequestProcessor's `UsesClaimService(platform)` GitHub-only check was removed. All structured webhooks with both ProjectName and Platform set now go through ITicketClaimService — the dead fire-and-forget `ExecuteStructuredAsync` method was deleted. Pre-p0095a behaviour is no longer reachable; the legacy TriggerInput path (PR comments) stays untouched as documented in p0095a.
- [Scope] ITicketProvider.ListByStatusAsync moved from p0095b to p0095c. It's a reconciler dependency, not a transitioner dependency — keeping it with the reconciler (p0095c) makes the p0095b YAML redundant with the multi-platform transitioner focus and gives p0095c the full "recovery" surface area in one coherent phase.

## p0095c: Lifecycle Recovery
- [Architecture] IJobHeartbeatService exposes both `Start(ticketId) → IAsyncDisposable` and `IsAliveAsync(ticketId)`. The second method keeps StaleJobDetector and EnqueuedReconciler in AgentSmith.Application (which doesn't reference StackExchange.Redis). Without IsAliveAsync they'd need direct IDatabase access, forcing them into Infrastructure and breaking the layering. Cost: one extra interface method; benefit: correct layering and tests don't need Redis mocks.
- [Architecture] ListByLifecycleStatusAsync defaults to empty on ITicketProvider. p0095c ships the GitHub impl (GetAllForRepository with label filter). GitLab/AzDO/Jira keep the default — reconciler becomes a no-op for them until a follow-up phase adds the listing implementations. This is incremental delivery: the recovery infrastructure is in place and exercised for GitHub, other platforms land when their implementations do.
- [Architecture] PipelineExecutor.BeginLifecycleAsync + LifecycleScope (`IAsyncDisposable`) wrap execution with Enqueued→InProgress transition + heartbeat start on entry, and InProgress→Done/Failed + heartbeat stop on disposal. `MarkFailed()` toggles the disposal target status. Scope.Noop is used when no TicketId is in context (manual CLI runs). The disposal runs transitions with CancellationToken.None because the scope must complete even when the pipeline was cancelled — we want the ticket marked Failed in the cancellation case, not left in InProgress.
- [Architecture] StaleJobDetector + EnqueuedReconciler run unconditionally on every replica in p0095c. Leader-election is deferred to p0096 (which introduces the lease primitive for the poller anyway). Duplicate detector/reconciler work is cheap because all transitions are atomic per platform — whichever replica wins sees the updated status and the others no-op on their next scan.
- [Scope] Full LifecycleConfig schema (status_labels, reconciler_interval_minutes, heartbeat_ttl_seconds) deferred to a follow-up. p0095c uses hardcoded intervals (heartbeat 30s/2min TTL, stale scan 1min, reconcile 10min) that match p0095a's implicit defaults. LifecycleConfig + Jira native-mode probing are naturally paired — they land together when someone wants per-project lifecycle label customisation.
- [Scope] OpenTelemetry counters deferred to the same follow-up. Structured logging at Information level covers the observability needs for a small team; metrics make sense when a platform operator team wants dashboards. Adding `System.Diagnostics.Metrics.Meter` is a small additive change when requested.
- [TradeOff] Hosted services (consumer, stale detector, reconciler) are plain classes with RunAsync(ct), not BackgroundService — matches p0095a's PipelineQueueConsumer decision. Cli has no IHost. Task.WhenAll in ServerCommand composes them. When/if Cli migrates to IHost, conversion is mechanical.

## p0096: Event Pollers
- [Architecture] Two independent Redis leases: `agentsmith:leader:poller` and `agentsmith:leader:housekeeping`. A single replica can hold both; having them separate means polling can go leader-elected without disturbing housekeeping timing, and vice versa. Same `LeaderElectedHostedService` runs both with different keys.
- [Architecture] LeaderElectedHostedService loop: TryAcquire → while leader { work, renew, check } → on renewal failure cancel work, release, back to try-acquire with 5s idle backoff. Ensures exactly-one-leader under Redis's TTL+CAS semantics.
- [Architecture] RedisLeaderLease uses CAS Lua for both RENEW (PEXPIRE only if stored value matches token) and RELEASE (DEL only on match). Prevents a GC-paused or network-partitioned leader from extending or deleting a lease the new holder acquired after the TTL elapsed — the classic "Redlock lite" pattern.
- [Architecture] Housekeeping services (StaleJobDetector + EnqueuedReconciler from p0095c) migrated from unconditional-per-replica to leader-only. Reduces N-fold API calls; correctness was already there via atomic transitions, but the extra calls were wasteful. ServerCommand's RunHousekeepingAsync runs both services in one Task.WhenAll under the housekeeping leader.
- [Architecture] PollerHostedService fan-outs pollers via Task.WhenAll with a 20s per-poller timeout. One slow platform does not block the others. Exceptions caught per poller and logged; one failure does not poison the cycle. Claims from all pollers are then processed sequentially — ClaimAsync is fast (few hundred ms worst case), and sequential processing keeps Redis lock contention predictable.
- [Architecture] IEventPoller reads via ITicketProvider.ListByLifecycleStatusAsync(Pending) — same dependency the reconciler uses. p0096 ships the GitHub poller only; other platforms follow when their ListByLifecycleStatus implementations land (same incremental pattern as p0095c).
- [Architecture] Polling opt-in per project via PollingConfig (default Enabled=false). Webhook-only deployments see no behaviour change. Disabled projects are skipped at BuildPollers time in ServerCommand — no null pollers flow into PollerHostedService.
- [Scope] Parallel-across-platforms, sequential-across-claims. Parallelism knob for ClaimAsync dispatch was considered but rejected — claim is O(ms) unless a platform API is slow, and semaphore complexity here is not worth a few hundred ms.
- [Scope] Jitter is per-cycle (±10%) applied after Task.WhenAll. Per-project jitter (spec option) was simpler at this scope — one randomised sleep at the end of each cycle, minimum across configured intervals.
- [Scope] Leader lease renewal interval 10s, TTL 30s. 3x safety margin matches the p0095c heartbeat pattern; if the process GC-pauses or clock-skews for up to 20s we still renew before the TTL hits.

## p0098: Docs Catch-Up for p0084 + p0095a/b/c + p0096
- [Architecture] `concepts/ticket-lifecycle.md` is the single canonical state-machine page. Setup and configuration pages link to it rather than restating the diagram, so future changes land in one place. Recovery semantics (StaleJobDetector, EnqueuedReconciler, what-survives-what) live there too — they're concept material, not setup steps.
- [Architecture] Polling docs split into two pages: `setup/polling.md` (config + ops + troubleshooting, action-oriented) and `setup/polling-vs-webhooks.md` (decision matrix, scenario-oriented). Operators reaching the docs from "I need to enable polling" land on the first; operators reaching from "should we use polling here?" land on the second. Single combined page tested in draft and felt overloaded.
- [Implementation] Polling page leads with a `!!! warning "GitHub-only at runtime"` admonition. Other docs (label-triggers, polling-vs-webhooks, agentsmith-yml) repeat the limitation in tables. Cost: redundancy. Benefit: an operator who skims a single page still sees the constraint — silent failure ("polling enabled, no claims") is the failure mode this guards against.
- [Architecture] `setup/README.md` reorganised into "Ticket Ingress" with Webhook + Polling as siblings, instead of the previous Webhook-only listing with Polling absent. Decision matrix lives in the comparison page; README only routes to it. Keeps README scan-friendly.
- [Scope] `architecture/index.md` got two table rows (Claim-then-Enqueue, Leader Election) in the Patterns section; `architecture/layers.md` got expanded service tables. Did not touch the layer diagram or the dependency-flow text — those still reference the old Host/Dispatcher names (renamed to Cli/Server in p0069) and should be a separate cleanup phase. Mixing layer-rename in this phase would dilute the docs catch-up message.
- [Scope] PR-comment webhook path (free-form TriggerInput, dialogue answers) explicitly called out in `webhooks.md` as separate from the lifecycle. Avoids confusion when an operator notices PR comments don't get `agent-smith:enqueued` labels.
- [Scope] Single commit `docs: catch up to p0084 + p0095a/b/c + p0096`. Splitting per-page would create intermediate docs states where one page describes the claim flow and the next still says "webhook → ExecutePipelineUseCase direct". Docs are read coherently or not at all.

## p0101: CLI server graceful degradation + per-subsystem health
- [Architecture] Resilience strategy split: webhook listener always up, Redis-dependent subsystems (queue_consumer, housekeeping, poller) degrade. Server boots with REDIS_URL unset OR Redis temporarily down. Webhook POSTs that hit ITicketClaimService while Redis is down respond 503 with body `redis_unavailable` instead of throwing. Operators see /health/ready=503 immediately — no silent-no-op state.
- [Architecture] ConnectionMultiplexer built with `AbortOnConnectFail=false`, `ConnectRetry=3`, `ConnectTimeout=5000ms`. Multiplexer reconnects automatically when Redis comes up. Single-line change vs old `ConnectionMultiplexer.Connect(url)`; no behaviour change in the happy path. Verified by `Build_RedisUnreachable_StillBuildsServiceProvider` test (asserts no throw within 15s for non-routable URL).
- [Architecture] Redis-dependent service registrations (`IRedisJobQueue`, `IRedisClaimLock`, `IRedisLeaderLease`, `IJobHeartbeatService`, `IConversationLookup`, `IMessageBus`, `IDialogueTransport→RedisDialogueTransport`) moved out of `AgentSmith.Infrastructure.ServiceCollectionExtensions` into `AgentSmith.Cli.ServiceProviderFactory.RegisterRedis`, gated on whether `IConnectionMultiplexer` was registered. Rationale: the existing `if (queue is null) return Task.CompletedTask` graceful-skip pattern in ServerCommand only works if those services are NOT in the container when Redis isn't available. Spec called for an `AddAgentSmithCommands(bool redisAvailable)` overload but `services.Replace(ServiceDescriptor.Scoped<ITicketClaimService, NullTicketClaimService>())` keeps Application clean and is less invasive.
- [Architecture] /health is liveness — HTTP 200 as long as the listener is alive (status field reflects degradation in the body). /health/ready is readiness with a strict loud-fail rule: ANY subsystem in non-Up state (Down, Degraded, Disabled) returns 503. Liveness vs readiness split keeps Kubernetes/Docker restart loops sane (don't kill the pod just because Redis is briefly down) while still surfacing the issue to readiness gates and alerting. Body lists every subsystem with name/state/reason/last_changed_utc so operators see WHY at a glance.
- [Architecture] ISubsystemHealth contract surface stays tiny: `Name`, `State` (Up/Degraded/Down/Disabled), `Reason`, `LastChangedUtc`. Each long-running subsystem owns a `SubsystemHealth` instance (mutable thread-safe impl in `AgentSmith.Application/Services/Health/SubsystemHealth.cs`). WebhookListener iterates the injected `IReadOnlyList<ISubsystemHealth>` to build the report. No reflection, no central registry — just DI plus an explicit list passed to the listener constructor.
- [Implementation] `SubsystemHealth` lives in `AgentSmith.Application/Services/Health/`, not `Contracts/Services/` as the spec sketched. Coding-principles say `Contracts/` is for interfaces only — placing the mutable impl in Application keeps Contracts pure. Spec deviation logged here.
- [Architecture] Subsystem retry loop (`SubsystemTask.RunRedisGatedAsync`): if `TService` is unregistered → SetDisabled, return (no point looping for a config that will never become true — distinguishes Disabled from Down/Degraded). If registered but multiplexer disconnected → SetDegraded("waiting for Redis"), poll IsConnected every `RedisRetryIntervalSeconds`. Once connected → SetUp, run inner work. On work error → SetDegraded("task error: {msg}"), re-enter retry loop. ct cancellation exits cleanly without retrying.
- [Implementation] NullTicketClaimService returns `ClaimResult.Failed("redis_unavailable")` rather than `ClaimResult.Rejected(...)`. The existing `ClaimRejectionReason` enum carries semantics "rejected claims will never succeed as-is — operator must fix config", which technically fits "operator must set REDIS_URL", but Failed-with-string-error is closer to "transient" and avoids growing the enum. `MapClaim` in WebhookRequestProcessor pattern-matches on `Failed when Error == "redis_unavailable"` → 503; everything else stays 500.
- [Implementation] WebhookRequestProcessor pre-checks `IsRedisAvailable()` (looks up the `redis` ISubsystemHealth from DI) before structured-claim dispatch and dialogue routing. Returns 503 + `redis_unavailable` immediately if redis state is not Up. Cheaper than try/catch on DispatchAsync and catches the case where Redis is configured but disconnected at the moment of webhook arrival.
- [Implementation] ServerCommand.BuildPollers extracted to `Cli/Services/PollerFactory.cs`. Reason: ServerCommand was already at the upper class-size limit; adding subsystem-health plumbing pushed it over. Extraction also models BuildPollers as a real responsibility (platform-switch poller construction) rather than a private detail of the command. Tests updated to call `PollerFactory.Build` directly.
- [Implementation] LeaderSubsystemRunner extracted (`Cli/Services/LeaderSubsystemRunner.cs`) to collapse the identical plumbing for `housekeeping` and `poller` subsystems (both: redis-gated → leader-elected → run inner work). ServerCommand orchestration shrinks from a duplicated pair of `StartXxxLeaderAsync` methods to a single `LeaderSubsystemRunner.RunAsync` call per subsystem.
- [Scope] `AgentSmith.Server` (Slack/Teams gateway) keeps its current strict `depends_on:redis` setup. It is a different deployment unit with stronger Redis coupling (Slack signing secrets, conversation state, dispatcher queue). Making it resilient is a follow-up phase. p0101 is strictly about the CLI `server` command.
- [Scope] WebhookListener integration tests via real HttpListener not written. The HTTP endpoint behaviour is covered by `HealthResponseBuilderTests` (JSON shape + status codes for all /health and /health/ready cases) plus the listener constructor wiring is verified by `ServiceProviderFactoryRedisGatingTests`. Manual smoke test documented in done criteria. End-to-end HttpListener tests would buy little extra confidence at significant complexity cost.
- [Scope] `WebhookRequestProcessor_StructuredWebhookRedisDown_Returns503` end-to-end test not written for the same reason — would require constructing a valid platform-specific webhook payload that survives WebhookPlatformDetector + signature verifier. The 503 mapping is unit-tested via `NullTicketClaimServiceTests` (returns Failed("redis_unavailable")) plus the static `MapClaim` mapping in WebhookRequestProcessor is straightforward. Trade-off documented; full-stack coverage available via the manual smoke test in done criteria.
- [Implementation] Logging cadence: INFO on subsystem state transitions (started, disabled, waiting for Redis, restored). WARN on task error. Goal: operator's tail always shows the last-event-state without flooding during outages.
- [Architecture] Null-Redis implementations (`AgentSmith.Application/Services/RedisDisabled/`) for `IRedisJobQueue`, `IRedisClaimLock`, `IRedisLeaderLease`, `IJobHeartbeatService`, `IConversationLookup`. Registered by ServiceProviderFactory in the no-REDIS_URL branch alongside NullTicketClaimService. Reason: p0101's initial pass only fixed the `server` command — `security-scan`, `fix`, `mad`, etc. crashed at DI resolution time because TicketStatusTransitionerFactory (constructor) and PipelineExecutor (LifecycleScope) inject Redis services transitively. With null impls, every CLI command resolves cleanly without Redis. Methods that genuinely need Redis (claim acquisition, queue enqueue, heartbeat start) throw `RedisUnavailableException` with an actionable message ("set REDIS_URL"). Methods with safe defaults (queue depth, heartbeat aliveness, conversation lookup) return 0/false/null. Verified by smoke-running `security-scan --project agent-smith` to skill-round stage without Redis configured.
- [Implementation] SubsystemTask gating switched from `GetService<T>() is null` to `redis ISubsystemHealth.State == Disabled`. Null impls always resolve to non-null instances, so the old null-check would never fire. The redis subsystem health (registered by RegisterRedis as Disabled when REDIS_URL is unset) is now the single source of truth for "Redis configured?". Test `RunRedisGatedAsync_RedisHealthDisabled_SetsDisabledAndReturns` updated accordingly.
- [TradeOff] NullRedisLeaderLease.TryAcquireAsync returns null silently (lease not acquired) rather than throwing. Reason: leader election is server-only — CLI commands inject the dep transitively but never invoke it. A throwing impl would be loud-fail in dead code paths. The server uses the redis ISubsystemHealth check via SubsystemTask before leader election even starts, so the null lease is never invoked in server mode either when REDIS_URL is unset.

## p0097: Parallel Skill Rounds
- [Architecture] DeferredBuffers context key as the channel between handler base and executor. SkillRoundHandlerBase always builds a `SkillRoundBuffer`; if the executor put a list under `ContextKeys.DeferredBuffers` before dispatching the batch, the handler appends to it and returns; otherwise it applies the buffer immediately (sequential default). Public `ICommandHandler.ExecuteAsync` signature unchanged — no new interface, subclass handlers (SkillRoundHandler/SecuritySkillRoundHandler/ApiSkillRoundHandler) stay one-liners. Alternatives considered: (a) new `ISkillRoundCollector` returning `(CommandResult, SkillRoundBuffer)` — adds plumbing to every handler; (b) explicit flag in PipelineContext queried by handlers — same idea, more verbose. Picked the channel approach because presence/absence IS the signal.
- [Architecture] Observation IDs assigned at merge time, not parse time. `ObservationParser.ParseWithoutIds` returns `Id=0` for every entry; `ApplyBufferToContext` reads the runtime list, computes `nextId = Max(o.Id) + 1`, and stamps each buffer entry under the executor's serial merge order. Parallel completion order does NOT affect IDs — graph order does. Cross-run ID stability is NOT claimed (LLMs vary observation count run-to-run); the contract is "deterministic within a single run."
- [Architecture] `PipelineBatchRunner` extracted from PipelineExecutor as a separate class (composition over fat orchestrator). Reason: PipelineExecutor was already over the class-size limit before parallelism arrived; adding fan-out + cancellation + buffer-merge would push it well past 500 lines. Extraction also models the runner as a real responsibility (parallel execution under throttle) instead of a private detail. PipelineExecutor now owns main loop + sequential single-step path + lifecycle; the runner owns SemaphoreSlim, linked CTS, and merge.
- [Architecture] `BatchOutcome` value object replaces a tuple-of-arrays return shape from the runner. The two queries operators care about post-batch — first failure (fail-fast reporting) and first InsertNext (sequential follow-up insertion) — sit on the outcome as named methods. Keeps PipelineExecutor's batch handler short and the intent legible.
- [Implementation] Buffer merge in graph order, not completion order. `MergeBuffersInGraphOrder` walks the original `LinkedListNode<PipelineCommand>` list and looks each skill name up in a `Dictionary<string, List<SkillRoundBuffer>>` built from the deferred list. Tasks may complete in any order, but merge order is fixed by the topology.
- [TradeOff] CancellationToken cancels orchestration but NOT in-flight LLM calls. Once the SemaphoreSlim releases and the inner `commandExecutor.ExecuteAsync` is past the point of return, Polly retries can still fire and Anthropic responses run to completion. Cost is recorded for every settled call (cancelled task's cost is tracked before the cancellation throws), so the cost report stays trustworthy. Operators should expect close-to-full batch cost even when one slot fails. Documented because "CTS = stop everything" is a reasonable misread.
- [TradeOff] `MaxCommandExecutions` (100) counts each command in the batch once. A 13-skill stage burns 13 of 100 in one parallel burst — same total as today's sequential path, just consumed in fewer wall-clock seconds. Acceptable for now; revisit if security-scan pipelines with nested OBJECTIONs trip the guard.
- [Implementation] PipelineCostTracker switched from `Interlocked.Add`/`Increment` to a `lock`-based design. `Interlocked` covered the int counters but `_lastModel` write was already racy and `EstimateCostUsd`/`ToString` read multiple fields non-atomically. One lock simplifies the model and the contention window is negligible relative to the LLM call.
- [Implementation] PipelineContext `Dictionary<string, object>` wrapped under a single lock. The deferred-buffer pattern means most parallel writes go through the locked `List<SkillRoundBuffer>` in DeferredBuffers, but handlers still call `pipeline.Set(ContextKeys.ActiveSkill, name)` during execution. ConcurrentDictionary was considered but would force callers using `Get<T>` to handle missing-key differently. Single lock keeps the surface identical.
- [Architecture] Parallelism opt-in via `agent.parallelism.max_concurrent_skill_rounds` (default 1). Sequential path is byte-identical to pre-p0097: same logging, same execution trail, same observation IDs given identical LLM output. Verified by full test suite (1089 → 1105, +16 new tests) all green at default.
- [TradeOff] Logging stays per-command in batch mode (each slot logs its own `[{Step}/{Total}] Executing ...` and `... completed`). With 13 parallel slots this can interleave on stdout — tolerable in CLI mode, potentially noisy on Slack/Teams progress streams. Spec flagged this for verification; no batch-aware progress event added in p0097. Revisit if UX feedback on production runs (with `MaxConcurrentSkillRounds=4`) shows actual flooding.
- [Scope] InsertNext semantics on batch: first-in-graph-order InsertNext wins. If skills A and B in the same batch both return InsertNext, only A's follow-ups are queued. Conservative choice — alternatives (concatenation, voting) considered too clever for the typical use case (OBJECTION from one skill triggering a single targeted re-round). Documented so future-self doesn't reinvent it.

## p0102: API Scan Code-Aware + Prefix Caching

- [Architecture] `ILlmClient.CompleteWithCachedPrefixAsync(systemPrompt, userPrefix, userSuffix, taskType, ct)` added as an optional default-method overload. Providers without prompt caching (OpenAI, Azure OpenAI, Gemini, Ollama) inherit the default that joins the two halves and falls through to `CompleteAsync`. `AnthropicLlmClient` overrides to send the user message as two `TextContent` blocks with `cache_control = ephemeral` on the prefix only. Alternative considered: a separate `ICachedLlmClient` interface — rejected because it would force two paths through `SkillRoundHandlerBase`/`GateRetryCoordinator` for every call.
- [Architecture] Prompt-prefix split lives in a new `PromptPrefixBuilder` service that owns the boundary "what's stable across same-round calls vs. what varies per skill." `SkillPromptBuilder` gained `BuildDiscussionPromptParts` / `BuildStructuredPromptParts` returning `(system, prefix, suffix)`; the existing single-string methods now delegate to the parts API and re-join. Subclasses opt into caching by overriding `SkillRoundHandlerBase.BuildDomainSectionParts(pipeline)` — default returns `(BuildDomainSection(pipeline), "")`, so existing `SkillRoundHandler` and `SecuritySkillRoundHandler` keep their old behavior with no edits.
- [Architecture] `ApiSkillRoundHandler.BuildDomainSectionParts` puts the swagger spec, code context, summary, and probe results in the stable half; mode line, per-skill findings slice, and per-skill source excerpts go in the variable half. The active-skill name is the only prompt input that differs across the same round, so per-skill code excerpts are scoped to the active role (auth-config-reviewer / ownership-checker / upload-validator-reviewer) — keeps the prefix identical across the cohort.
- [Architecture] `GateRetryCoordinator.ExecuteAsync` signature changed from `(systemPrompt, userPrompt, …)` to `(systemPrompt, userPromptPrefix, userPromptSuffix, …)`. Both attempts (initial + retry) reuse the same prefix; only the suffix grows the corrective-prompt block. Cache hit on retry is preserved.
- [Architecture] `EvidenceMode` extended with a third value `AnalyzedFromSource` (in addition to `Potential` and `Confirmed`). `Finding.DisplayLocation` was tweaked to prefer `File:StartLine` when the mode is `AnalyzedFromSource`, even if `ApiPath` is also set — code-evidence findings should always surface their file:line. Console / Markdown / SARIF formatters render a three-way badge.
- [Architecture] `ApiCodeContextHandler` runs after `LoadSwagger` and before `SessionSetup` in the `ApiSecurityScan` preset. When `ContextKeys.SourcePath` is unset, it sets `ApiSourceAvailable = false` and returns immediately — the rest of the pipeline behaves identically to pre-p0102. When source is present, it walks the tree once via `IRouteMapper`, `IAuthBootstrapExtractor`, and `IUploadHandlerExtractor`, then exposes the populated `ApiCodeContext` for downstream skills.
- [Architecture] `RouteMapper` walks the source tree once and accumulates declarations per-framework. `FrameworkRoutePatterns` carries per-language file-extension scoping (`.cs` for .NET, `.js/.ts` for Express, `.py` for FastAPI, `.java/.kt` for Spring) so the patterns can't cross-match each other (an early test failure: `@app.post(...)` in a `.py` file matched the Express regex). Path canonicalization replaces `:param` (Express) and `{param}` (everything else) with `{}` so swagger paths and source paths compare correctly.
- [Architecture] Confidence scoring on `RouteHandlerLocation`: exact method+path match → 1.0, path match with method mismatch → 0.5, no path match → not in list. Decision-locked threshold for downstream skills is 0.5 — entries below that are kept in `RoutesToHandlers` (so the run output can log them as "low confidence") but skills must not emit findings against them. Implemented in `ApiSkillRoundHandler.BuildPerSkillCodeSection` and codified in `api-security-principles.md`.
- [Architecture] `ApiSecurityTriageHandler` was over the 120-line class limit, so the gate landed as three classes: `ApiSecurityTriageHandler` (orchestration, ≤60 lines), `ApiSecurityTriagePromptBuilder` (signal + LLM prompt assembly), `ApiSecuritySkillFilter` (mode + source-availability gate). `Filter()` returns `{recon-analyst, anonymous-attacker, false-positive-filter, chain-analyst}` for passive+no-source, drops `auth-config-reviewer / ownership-checker / upload-validator-reviewer` whenever source is absent, and otherwise returns the input list unchanged. Active-mode-capable contributors (idor-prober, low-privilege-attacker, input-abuser, response-analyst) keep their existing degrade-in-passive behavior outside the passive+no-source branch.
- [Architecture] `config/patterns/api-auth.yaml` carries 8 deterministic patterns (the four `ValidateXxx = false` JWT toggles, CORS `AllowAnyOrigin + AllowCredentials` combo, `[AllowAnonymous]` adjacent to a state-changing verb, hardcoded JWT signing keys, exception-message-in-response). LLM skills cover the nuance; static patterns catch the unambiguous bugs cheaply. Same split that p0094b applied to `auth.yaml`.
- [Implementation] The three new `IRouteMapper` / `IAuthBootstrapExtractor` / `IUploadHandlerExtractor` interfaces live in `AgentSmith.Contracts.Services`; their regex-based implementations live in `AgentSmith.Infrastructure/Services/Security/Code/`, alongside a small `SourceSnippetReader` helper for bounded-line-range reads. Roslyn-based mapping was deferred per spec decision (#19) — revisit if LLM under-confidence on nuanced cases is measurable.
- [Implementation] `agentsmith.example.yml` gained a sibling `api-security` project block (alongside `my-project`) with `parallelism.max_concurrent_skill_rounds: 4` and a `source.path` entry. The comment in the parallelism field explains *why* 4 is required (5-minute Anthropic cache TTL — sequential calls for 13 contributors take longer than the TTL window, so the cache misses on later skills). The example was added rather than editing `my-project` to keep the basic example minimal.
- [Implementation] CLI banner now prints `mode | source | ~N skill(s)`. Skill count is estimated locally in the CLI from `(active, source)` — kept as a static map rather than asking the triage handler, because triage runs per-pipeline and the banner fires before pipeline DI exists.
- [Scope] LLM-skill content tests (`AuthConfigReviewer_DeadAuthorizationMiddleware_Critical`, `OwnershipChecker_DbSetWithoutUserPredicate_FlagsIdor`, `UploadValidatorReviewer_HeaderOnlyMime_Medium`) check that `SKILL.md` mentions the keywords the spec calls out, rather than running the LLM. Full skill behaviour requires an integration run against a fixture repo; the spec calls those out in the done criteria as run-time evidence rather than unit tests.
- [Scope] Baseline and Comparison cost decision keys (per spec done-criterion) are NOT filled in this commit. Recording them honestly requires a real api-scan run against a fixture target with an Anthropic API key; that run produces the numbers and a follow-up commit appends them here, pinned to model ID + `MaxConcurrentSkillRounds` value (matching p0094b's pattern). Placeholder follow-up tracked below; do not interpret the absence as zero cost benefit.
- [TradeOff] `MaxConcurrentSkillRounds = 4` in the example config is the spec-required default for api-security-scan but is not the system-wide default. Operators running api-scan without copying the example config get the safe sequential path (cache miss on later skills, no exposure to parallel-call edge cases) — they only opt in once they read the comment. Trade: docs-as-default vs. behavior-as-default. Chose docs because the cache-TTL constraint is project-specific.

### Baseline / Comparison (deferred — to be filled by a follow-up commit)
- Baseline (without prefix caching, MaxConcurrentSkillRounds=4, Sonnet 4): TBD wall-clock, TBD tokens, TBD USD
- Comparison (with prefix caching, same config): TBD wall-clock, TBD tokens, TBD USD, TBD % of baseline


## p0102a: API Scan Source Checkout

- [Architecture] `TryCheckoutSourceHandler` mirrors `CheckoutSourceHandler` but is fail-soft. Two handlers with shared `ISourceProviderFactory` is clearer than one handler with a policy flag — security-scan keeps strict semantics (any clone failure is fatal), api-scan never fails the pipeline. The handler returns `Ok` in every branch; downstream `ApiCodeContextHandler` decides what to do based on whether `ContextKeys.SourcePath` is set.
- [Architecture] Single source-of-truth contract downstream: `ContextKeys.SourcePath`. Whether the path comes from `--source-path` (CLI), a configured Local source, or a fresh remote clone, every consumer reads the same key. `ContextKeys.Repository` (set by the strict `CheckoutSourceHandler`) is intentionally not introduced into the api-scan flow — keeps one mechanism, one read site.
- [Architecture] Branch resolution differs from the strict `CheckoutSourceContextBuilder`. api-scan has no `TicketId` in scope, so `BranchName.FromTicket` does not apply and `ContextKeys.CheckoutBranch` (set by webhooks) is also ignored. Branch comes from `SourceConfig.DefaultBranch` only; if unset, the provider's default ('main') wins. Read-only inspection — checking out a ticket-scoped branch would be wrong here.
- [Implementation] Auth presence is not pre-checked from `SourceConfig.Auth`. `SourceProviderFactory.CreateGitHub` (and the GitLab/AzureRepos siblings) ask `SecretsProvider.GetRequired("GITHUB_TOKEN")` which can resolve via env vars, secret stores, or any other registered source. A naive `if (string.IsNullOrEmpty(source.Auth))` guard would shadow valid auth paths. Instead the handler wraps `factory.Create(source) + provider.CheckoutAsync(...)` in try/catch and treats any failure (missing token, network unreachable, invalid URL, transient git error) the same way: log warning, fall back to passive mode.
- [Implementation] Banner moved to two phases. Pre-flight in `ApiScanCommand` shows mode + 'Resolving source...'; the final 'Source: <path or unavailable> | ~N skill(s)' is emitted by `TryCheckoutSourceHandler` via `ILogger.LogInformation` after each branch decides. `EstimateSkillCount` moved out of the CLI into the handler so the count reflects the actual resolved state, not the CLI flag's pre-flight guess. No event-subscriber plumbing — direct logger output is sufficient.
- [Architecture] Conditional execution lives in the handler, not in pipeline composition. `PipelinePresets.ApiSecurityScan` is a static list with `TryCheckoutSource` always first; the handler decides at runtime whether the source is configured/reachable and shortcircuits to passive mode if not. Dynamic step injection was considered (and discussed in spec) but rejected — it would have been a larger architectural change for no semantic gain in this case.
- [Implementation] Clone caching is provider-owned, not phase-owned. `ISourceProvider` implementations (GitHub/GitLab/AzureRepos) clone into `Path.GetTempPath()/agentsmith/<owner>/<repo>` and short-circuit re-clone via `LibGit2Sharp.Repository.IsValid`. Same persistent cache `security-scan` already uses; api-scan inherits the convention as-is. No new cleanup hook in `TryCheckoutSourceHandler` — disposal is the user's concern. Stale-cache risk (out-of-date local clone vs upstream) exists today and is not regressed by this phase.
- [Scope] `ApiCodeContextHandler` was not modified beyond a doc-comment refresh. The existing `TryResolveSourcePath` already reads `ContextKeys.SourcePath` and is agnostic to who set it — the handler reads the same key whether the value came from the CLI flag, the Local source resolution, or a remote clone result. One read site, three producers.

## p0106: Multi-Pipeline Projects

- [Architecture] `ProjectConfig.Pipelines: List<PipelineDefinition>` + `ProjectConfig.DefaultPipeline: string?` joined the existing `Pipeline` / `SkillsPath` fields (which stay as legacy shims, not a breaking change). Each `PipelineDefinition` carries a name plus optional overrides for `Agent`, `SkillsPath`, `CodingPrinciplesPath`. Eliminates duplicated project blocks — `config/agentsmith.yml` still has four `agent-smith-*` projects on the same repo, but new configs can collapse them into a single multi-pipeline project.
- [Architecture] Single resolver `IPipelineConfigResolver.Resolve(project, pipelineName) → ResolvedPipelineConfig`. ExecutePipelineUseCase resolves once at the top of every run and stashes the merged view under `ContextKeys.ResolvedPipeline`; downstream builders/handlers read it via the `pipeline.Resolved()` extension method — never directly from `ProjectConfig`. ~30 read sites for `project.Agent` / `project.SkillsPath` / `project.CodingPrinciplesPath` migrated; the resolver is the single merge function rather than 30 inline `?? project.X` fallbacks.
- [Architecture] Resolver tolerates undeclared pipeline names — returns a definition synthesized from project defaults. Reason: `init-project` (CLI bootstrap) and other "free-form" pipeline names that aren't in `pipelines:` still need to work. Strict "throw if not declared" was rejected because it broke `init` and made test ergonomics painful for `new ProjectConfig { Pipeline = "fix-bug" }`. Validation that *trigger* references resolve happens at config load instead — that's where misspelled names get caught.
- [Architecture] Skills-path fallback chain: `pipelineDef.SkillsPath` → `PipelinePresets.GetDefaultSkillsPath(pipelineName)` (`security-scan` → `skills/security`, `api-security-scan` → `skills/api-security`, `legal-analysis` → `skills/legal`, `mad-discussion` → `skills/mad`, fall-through `skills/coding`). Fixes the pre-phase asymmetry where 5 CLI subcommands set `SkillsPathOverride` from the pipeline name but webhook/polling paths fell back to `project.SkillsPath` (default `"skills/coding"`). The directory-naming convention is the binding; explicit `pipelines[].skills_path` is opt-in for non-standard paths.
- [Architecture] `ContextKeys.SkillsPathOverride` deleted. The 5 CLI subcommands (Autonomous/ApiScan/Mad/SecurityScan/Legal) no longer set it; the 4 context builders (EvaluateSkills/Discover/Install/LoadSkills under ApiSecurity) read from `ResolvedPipelineConfig.SkillsPath` only. The override was a workaround for the asymmetry above; resolver subsumes it. Two truth sources collapsed to one.
- [Architecture] `pipeline.Resolved()` extension method on `PipelineContext` (in `Contracts.Commands`) over per-site `pipeline.Get<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline)`. Reads the same well-known key but compresses the call site. Static extension method is one of the two patterns coding-principles allows (alongside `Map()` helpers).
- [TradeOff] Backward-compat via `ProjectConfigNormalizer` invoked from `YamlConfigurationLoader`, not a breaking change. Legacy `pipeline:` + `skills_path:` fields populate a synthetic single-element `Pipelines` list and `DefaultPipeline` at load time. Existing configs run unchanged. Forcing migration in the same phase that introduces the mechanism would have doubled blast radius and review surface — the migration of `config/agentsmith.yml`'s four `agent-smith-*` projects to a single multi-pipeline project is deferred to follow-up `p0106a`.
- [Architecture] `pipelines:` is a **sparse override list, not a strict allowlist** (revised after first deployment). Each entry declares per-pipeline overrides; pipelines without entries inherit project defaults. Labels in `pipeline_from_label` may route to ANY system pipeline (defined by `PipelinePresets`), not just declared ones. Original strict-allowlist design (introduced and then revised in the p0106 fix) broke legacy configs whose `pipeline_from_label` routed to multiple pipelines while only one was synthesized from `pipeline:`. The sparse-override semantics matches the user's mental model: declare overrides where you need them, leave the rest implicit.
- [Implementation] Legacy-shim merges instead of skip-if-non-empty. If `pipeline:` (legacy) is set AND `pipelines:` already has entries, the legacy entry is added (deduplicated by name) and `DefaultPipeline` is set from `pipeline:`. Lets users mix both forms: declare overrides for specific pipelines while keeping `pipeline: fix-bug` as the default. Resolver also synthesizes-on-the-fly when `Pipelines` is empty but legacy `Pipeline` is set, so direct `new ProjectConfig { Pipeline = "fix-bug" }` test setups don't need to call the normalizer manually.
- [Implementation] Load-time validation is narrow: only `ProjectConfig.DefaultPipeline` must reference a declared pipeline. `pipeline_from_label` values and trigger-level `default_pipeline` are NOT validated against the declared list — they may route to any system pipeline. Catching typos in those is the responsibility of `PipelinePresets.TryResolve` at runtime (returns null for unknown names). Earlier strict trigger validation was removed after a real-world config (`config/agentsmith.yml` `azuredevops_trigger`) failed to load.
- [Scope] `Parallelism` stays inside `AgentConfig`, not lifted to `PipelineDefinition`. Per-pipeline parallelism is achievable today by overriding the whole Agent block. Premature extraction violates YAGNI; if usage shows users want different parallelism with the same Agent, extract it then.
- [Implementation] Pipeline-selection chain when no explicit name is supplied: `--pipeline` flag (CLI only) → `ProjectConfig.DefaultPipeline` → single-element shortcut (`Pipelines.Count == 1`) → throw `InvalidOperationException` listing declared pipelines. Loader-shim sets `DefaultPipeline = legacy.Pipeline` when migrating, so single-pipeline configs preserve ergonomics. Distinct from `WebhookTriggerConfig.DefaultPipeline` — same idea, different scope (webhook label-routing fallback). `ExecutePipelineUseCase`, `EnqueuedReconciler`, `AzureDevOpsWorkItemPoller`, and `WebhookRequestProcessor` route through this chain; the previous hardcoded `"fix-bug"` fallback in the AzDO poller is gone.
- [Implementation] Behavior-preservation gate: `MultiPipelineBehaviorPreservationTests` loads an inline copy of the four `agent-smith-*` projects from `config/agentsmith.yml` through the full loader+normalizer+resolver pipeline and asserts the 16 baseline tuples (pipelineName, Agent.Type, SkillsPath, CodingPrinciplesPath) match byte-for-byte. Same shape as p0094b's `Baseline:` key. Any drift is a regression detectable at PR time.
- [Implementation] Server DI was missing `IPromptCatalog`, `IRedisClaimLock`, `IRedisJobQueue`, `IRedisLeaderLease`, `IJobHeartbeatService`, `IConversationLookup`, `IDialogueTransport` (latent bug pre-dating p0106 — uncovered by `ASPNETCORE_ENVIRONMENT=Development` enabling `ValidateOnBuild`). `Server.Extensions.ServiceCollectionExtensions.AddRedis` extended to register all Redis-gated services; `AddCoreDispatcherServices` registers `IPromptCatalog` + `IPromptOverrideSource` directly (not via `AddAgentSmithCommands` — that pulls in the entire Application layer's pipeline handlers, over-eager for the dispatcher's needs).
- [Implementation] `IAgentProviderFactory` registration changed from Singleton to Scoped to fix lifetime violation against Scoped `IDialogueTrail`. Factory is a stateless dispatch over a creator-dictionary; per-scope allocation is cheap and matches the natural per-conversation/per-pipeline-run scope. Pre-existing latent bug, also uncovered by `ValidateOnBuild`.

## p0107: Server Consolidation

- [Architecture] Webhook handling, polling, queue consumer, lifecycle reconcilers all relocated from `AgentSmith.Cli` (`server` subcommand) into `AgentSmith.Server`. Server is now the single long-running deployment; CLI is a pure console-tool for one-shot pipeline runs and ad-hoc commands. Pre-phase: dual-container K8s pattern (Dispatcher + CLI-with-`server`) was historical drift from p0014 (webhook listener added to CLI when Dispatcher rename came later in p0069) — never had architectural justification. Post-phase: one process, one Kestrel, one DI tree.
- [Architecture] `WebhookListener` (raw `HttpListener` wrapping its own HTTP server) deleted. Webhook routes register directly on Server's existing `WebApplication` via a new `MapWebhookEndpoints` extension alongside `MapSlackEndpoints` / `MapTeamsEndpoints`. One Kestrel hosts three endpoint groups; behavior-preservation maintained by routing the same `(path, body, headers)` signature into the same `WebhookRequestProcessor.ProcessAsync` flow.
- [Architecture] Three thin `BackgroundService` wrappers in `Server.Services.Hosting/` absorb the `Task.WhenAll` orchestration that previously lived in CLI's `ServerCommand`: `QueueConsumerHostedService` (PipelineQueueConsumer), `HousekeepingLeaderHostedService` (StaleJobDetector + EnqueuedReconciler under `agentsmith:leader:housekeeping`), `PollerLeaderHostedService` (PollerHostedService under `agentsmith:leader:poller`). Each exposes its `ISubsystemHealth` instance as a singleton for the `/health` endpoint. The Application-layer services themselves stayed put — only the *wiring* relocated.
- [Architecture] DI consolidates: Server's `ServiceCollectionExtensions` gains `AddWebhookHandlers()` (registers all 13 `IWebhookHandler` implementations) and `AddLongRunningServices()` (registers the three hosted services + their healths + RedisConnectionHealth). `AddCoreDispatcherServices` calls `AddAgentSmithCommands` so the full Application pipeline-execution tree (ExecutePipelineUseCase, IPipelineConfigResolver, ITicketClaimService, all command handlers) is reachable from the WebhookRequestProcessor. The patchwork-fix from p0106's deployment hotfix collapses into a coherent extension-method chain in `Program.cs`.
- [Implementation] CLI's `ServiceProviderFactory.Build` shrunk to two modes: interactive (Console-based dialogue + progress, default) and spawned-job (Redis-backed when both `jobId` and `redisUrl` are non-empty — the path used by K8s job containers spawned by the Dispatcher). The previous mode-switching factory `RegisterProgressReporter` and the Redis-or-Null fallback in `RegisterRedis` deleted; CLI no longer carries server-side concerns.
- [Implementation] Duplicate `src/AgentSmith.Server/Services/RedisProgressReporter.cs` (a 3-line `global using` shim) deleted; `src/AgentSmith.Infrastructure/Services/Bus/RedisProgressReporter.cs` is the canonical implementation. The shim was a leftover from a long-past rename — invisible cruft caught by the move audit.
- [Implementation] `ILlmClient` registration added to `AgentSmith.Application/Extensions/ServiceCollectionExtensions.cs` as a latent-gap fix exposed by the new `ServerDiLifetimeTests`. Several handlers (EvaluateSkillsHandler, CompileKnowledgeHandler, QueryKnowledgeHandler) take `ILlmClient` directly; pre-phase only `ILlmClientFactory` was registered. Test caught it because Server's full validate-on-build now exercises the path. Default `Claude` config used since these handlers are typically configured per-project at runtime — same defaulting pattern as `LlmIntentParser`.
- [Implementation] New `ServerDiLifetimeTests` regression-guards Server's full DI tree by mirroring `Program.cs`'s registration order and calling `BuildServiceProvider(ValidateOnBuild=true, ValidateScopes=true)`. Uses existing `Null*` Application implementations + Moq for `IConnectionMultiplexer` / `IDialogueTransport` / `IJobSpawner` / `IProgressReporter`. The test caught both the pre-existing `ILlmClient` gap and verified that p0106's `IAgentProviderFactory→Scoped` fix sticks. Future Server-DI changes that introduce lifetime violations or missing services fail this test at build time.
- [Implementation] `Program.cs` declares `public partial class Program;` at the bottom. Enables `WebApplicationFactory<Program>` in future integration tests (deferred — not in scope for this phase, but the marker is in place).
- [Scope] Detailed user-facing webhook docs (curl examples, port references) NOT updated since URL routes (`/webhook`, `/webhook/{platform}`) and HTTP semantics are byte-for-byte identical. `docker-compose.yml` collapsed: deleted the `agentsmith-server` service (CLI with `server` cmd), updated header comment. `deploy/k8s/9-ingress.yaml` is unchanged — its `path: /` Prefix already routes webhook URLs to the Server service. No second deployment needed.
- [Scope] `ServiceProviderFactoryRedisGatingTests` deleted — they tested CLI's Redis-or-null fallback, which is now Server's responsibility (the Server-side equivalent is `ServerDiLifetimeTests`). 1206 tests post-phase (was 1210; net -4 from the deleted CLI gating tests +4 from new resolver/server tests ≈ wash).

## p0109: Jira Lock Decorator

- [Architecture] `IRedisClaimLock` is a multi-writer-coordination concern, not a Jira-platform concern. Cross-process serialization is needed only in the Server cluster (multiple pods, poller + webhook racing on the same ticket). A single CLI process cannot race with itself, and parallel CLI invocations have no shared Redis to coordinate through anyway. The lock attaches at composition time via a decorator (`LockedTicketStatusTransitioner`) that the Server registers and the CLI does not — Jira's API atomicity gap (no If-Match on `fields.labels`) is real, but the *need* for the workaround is conditioned on the multi-writer reality, which is a deployment property, not a platform property.
- [Architecture] Decorator wraps Jira only, not all platforms. GitHub/GitLab use ETag/If-Match-equivalent semantics; AzDO uses `op:test` on `/rev` for optimistic concurrency at the API level. Wrapping them with the same lock would convert API-level retries into queueing — strictly worse. The decorator class accepts any `ITicketStatusTransitioner` as inner type, but the `LockingTicketStatusTransitionerFactory` only applies it to `config.Type == "jira"`.
- [Implementation] DI rebind extracted from `AddRedis()` into a standalone `AddJiraLabelLockDecorator()` extension method. Reason: real Server `Program.cs` calls `AddRedis().AddCoreDispatcherServices()`, but `AddCoreDispatcherServices` internally calls `AddAgentSmithInfrastructure` which re-registers `ITicketStatusTransitionerFactory→TicketStatusTransitionerFactory`. MS DI is last-wins for `GetService<T>`, so an in-`AddRedis` rebind would be silently overwritten by the subsequent infrastructure registration. Standalone method called *after* `AddCoreDispatcherServices` makes the override stick. Test fixture `ServerDiLifetimeTests` mirrors the same call order.
- [Implementation] `LockingTicketStatusTransitionerFactory` takes the **concrete** `TicketStatusTransitionerFactory` (not `ITicketStatusTransitionerFactory`). Reason: passing the interface would risk a self-referential cycle if the binding is rebound to Locking. Binding the concrete type explicitly via `sp.GetRequiredService<TicketStatusTransitionerFactory>()` makes the dependency unambiguous. Infrastructure registration adds `services.AddSingleton<TicketStatusTransitionerFactory>()` plus a forwarding lambda for the interface so resolves of either return the same instance.
- [TradeOff] `NullRedisClaimLock` deleted as a Pre-p0107 leftover — it was never wired in production, conflicting with p0107's "no Redis-fallback in CLI" decision. Doc-comment-claimed use-case ("manual security-scan, fix without ticket lifecycle inject the dep but never call these methods") was based on the assumption that `IRedisClaimLock` had to be in the CLI graph at all; this phase removes that need entirely. `ServerDiLifetimeTests` fixture rebased onto `Mock.Of<IRedisClaimLock>()`. Sibling `Null*` classes in the same directory (`NullRedisJobQueue`, `NullJobHeartbeatService`, etc.) left untouched — they're still used by the test fixture as DI test doubles, and removing them is out of scope.
- [TradeOff] `EnvVarCollection` xUnit collection introduced to serialize tests that mutate process-global env vars. Reason: new `LockingTicketStatusTransitionerFactoryTests` and `CliShapedDiTests` set `GITHUB_TOKEN`/`GITLAB_TOKEN`/etc. so the inner factory's `SecretsProvider` calls succeed; existing `FactoryTests.TicketProviderFactory_NewTypes_RecognizedButNeedSecrets` asserts the *opposite* (env unset → ConfigurationException). xUnit's default per-class parallelism caused races. All three classes now share `[Collection(EnvVarCollection.Name)]`, and the env-mutating ones implement `IDisposable` to reset to clean state. Cheaper than introducing a `SecretsProvider` abstraction across the codebase for a test-only seam.
