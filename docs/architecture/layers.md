# Layer Details

Each layer in Agent Smith has a clear responsibility and strict dependency boundaries.

## Domain

**Project:** `AgentSmith.Domain` | **Dependencies:** None

The innermost layer. Contains business entities, value objects, and domain exceptions. No framework references, no external packages.

### Entities

| Entity | Purpose |
|--------|---------|
| `Ticket` | Issue/work item from any source (GitHub, GitLab, Azure DevOps, Jira) |
| `Repository` | Git repository reference with local path and remote URL |
| `Plan` | Execution plan with ordered steps |
| `PlanStep` | Single step within a plan |
| `PlanDecision` | Decision made during planning (approve/reject/modify) |
| `CodeAnalysis` | Analysis results from code review |
| `CodeChange` | A single file change (path, content, operation) |
| `AttachmentRef` | Reference to an attached document |

### Value Objects

| Value Object | Purpose |
|-------------|---------|
| `TicketId` | Strongly-typed ticket identifier |
| `BranchName` | Git branch name with validation |
| `FilePath` | File path with normalization |
| `ProjectName` | Project identifier |
| `CommandResult` | Success/failure result of a command |
| `PipelineCommand` | Command to execute in a pipeline |

### Exceptions

| Exception | When |
|-----------|------|
| `AgentSmithException` | Base exception for all domain errors |
| `ConfigurationException` | Invalid or missing configuration |
| `ProviderException` | External provider failure |
| `TicketNotFoundException` | Ticket does not exist |

---

## Contracts

**Project:** `AgentSmith.Contracts` | **Dependencies:** Domain

Defines all interfaces, commands, DTOs, and configuration models. This is the "contract" between Application and Infrastructure — neither depends on the other directly, both depend on Contracts.

### Commands

| Type | Purpose |
|------|---------|
| `ICommandHandler<TContext>` | Interface for pipeline step handlers |
| `ICommandExecutor` | Dispatches commands to handlers |
| `ICommandContext` | Base context passed through the pipeline |
| `PipelineContext` | Shared state bag for the entire pipeline run |
| `CommandNames` | Constants for all command names |
| `PipelinePresets` | Pipeline step definitions (fix-bug, security-scan, etc.) |

### Provider Interfaces

| Interface | Purpose |
|-----------|---------|
| `IAgentProvider` | AI agent (Claude, GPT-4, Gemini, Ollama) |
| `IAgentProviderFactory` | Creates agent providers per project config |
| `IContainerRunner` | Runs tool containers (Docker, Podman) |
| `IModelRegistry` | Per-task model selection |

### Configuration Models

| Model | Purpose |
|-------|---------|
| `AgentSmithConfig` | Root configuration object |
| `ProjectConfig` | Per-project settings (source, tickets, AI) |
| `AgentConfig` | AI provider settings (model, temperature, tokens) |
| `ModelRegistryConfig` | Model assignments per task type |
| `PricingConfig` | Token pricing for cost tracking |
| `SkillConfig` | Multi-skill pipeline definitions |
| `SourceConfig` | Git source provider settings |
| `TicketConfig` | Ticket provider settings |

### Other

| Type | Purpose |
|------|---------|
| `Finding` / `FindingSummary` | Security scan results |
| `RunCostSummary` | Token usage and cost data |
| `OutputContext` | Output strategy parameters |
| `ParsedIntent` | Result of intent parsing |
| `IDecisionLogger` | Records decisions made during execution |

---

## Application

**Project:** `AgentSmith.Application` | **Dependencies:** Contracts

The use-case layer. Contains all pipeline handlers, the pipeline executor, and supporting services. No external SDK references.

### Core Services

| Service | Purpose |
|---------|---------|
| `ExecutePipelineUseCase` | Resolves config, builds pipeline, executes — invoked by the queue consumer |
| `PipelineExecutor` | Runs an ordered list of commands; wraps execution with lifecycle transitions and heartbeat |
| `CommandExecutor` | Dispatches a single command to its handler |
| `CommandContextFactory` | Creates typed contexts for each handler |
| `PipelineCostTracker` | Aggregates token/cost data across handlers |
| `TicketClaimService` | Single ingress for ticket-driven pipelines: pre-checks → SETNX claim-lock → status transition → enqueue |
| `PipelineQueueConsumer` | Pulls `PipelineRequest` from `IRedisJobQueue`, runs them with `SemaphoreSlim` backpressure |

### Lifecycle & Polling Services

| Service | Purpose |
|---------|---------|
| `JobHeartbeatService` (Infrastructure) | Renews `agentsmith:heartbeat:{id}` every 30s; `IAsyncDisposable` clears on stop |
| `StaleJobDetector` | Reverts InProgress tickets without heartbeat back to Pending (every 1min, leader-only) |
| `EnqueuedReconciler` | Re-enqueues orphan Enqueued tickets (every 10min, leader-only) |
| `PollerHostedService` | Runs configured `IEventPoller` instances in parallel under the poller leader |
| `LeaderElectedHostedService` | Generic leader-election wrapper; runs work callback only when holding the named Redis lease |

The ingress/lifecycle stack: webhook handler (or `IEventPoller`) → `TicketClaimService` → `IRedisJobQueue` → `PipelineQueueConsumer` → `ExecutePipelineUseCase` → `PipelineExecutor`. See [Ticket Lifecycle](../concepts/ticket-lifecycle.md) for the state machine.

### Handlers (39 total)

Each handler implements `ICommandHandler<TContext>` and handles one pipeline step:

**Source & Setup:**

- `CheckoutSourceHandler` — Clone/pull repository
- `FetchTicketHandler` — Load ticket from provider
- `BootstrapProjectHandler` — Detect language, generate context
- `BootstrapDocumentHandler` — Prepare document for legal analysis
- `AcquireSourceHandler` — Acquire source for legal pipeline
- `LoadCodeMapHandler` — Generate/load code map
- `LoadContextHandler` — Load `.agentsmith/` context files
- `LoadCodingPrinciplesHandler` — Load domain-specific rules/skills
- `LoadSkillsHandler` — Load skill definitions for multi-skill rounds
- `LoadSwaggerHandler` — Load and compress OpenAPI specs

**Analysis & Planning:**

- `AnalyzeCodeHandler` — AI-driven code analysis
- `TriageHandler` / `SecurityTriageHandler` / `ApiSecurityTriageHandler` — Classify and prioritize
- `GeneratePlanHandler` — Create execution plan from analysis

**Execution:**

- `AgenticExecuteHandler` — Run the agentic code modification loop
- `SkillRoundHandler` / `SecuritySkillRoundHandler` / `ApiSkillRoundHandler` — Multi-skill execution rounds
- `ConvergenceCheckHandler` — Check if discussion/analysis has converged
- `SwitchSkillHandler` — Transition between skills in multi-skill pipelines
- `SpawnNucleiHandler` — Run Nuclei security scanner
- `SpawnSpectralHandler` — Run Spectral OpenAPI linter
- `ApprovalHandler` — Gate for human approval

**Output:**

- `TestHandler` — Run project tests
- `CommitAndPRHandler` — Commit changes, create pull request
- `WriteRunResultHandler` — Write run result to `.agentsmith/runs/`
- `CompileDiscussionHandler` — Compile multi-skill discussion into output
- `CompileFindingsHandler` — Compile security findings
- `DeliverOutputHandler` / `DeliverFindingsHandler` — Deliver results via output strategies
- `GenerateDocsHandler` / `GenerateTestsHandler` — Generate documentation/tests

### Intent Parsing

| Service | Purpose |
|---------|---------|
| `RegexIntentParser` | Fast pattern-based intent recognition |
| `LlmIntentParser` | LLM-based fallback for ambiguous input |

### Triggers

| Service | Purpose |
|---------|---------|
| `InboxPollingService` | Polls for new legal documents |

---

## Infrastructure.Core

**Project:** `AgentSmith.Infrastructure.Core` | **Dependencies:** Contracts

Shared infrastructure that does not require external SDKs. Configuration loading, project detection, and registries.

### Services

| Service | Purpose |
|---------|---------|
| `YamlConfigurationLoader` | Loads and validates YAML config files |
| `SecretsProvider` | Resolves secrets from environment variables |
| `ProjectDetector` | Detects project type (language, framework) |
| `ContextGenerator` | Generates `.agentsmith/context.yaml` |
| `CodeMapGenerator` | Generates code map from repository |
| `CodingPrinciplesGenerator` | Detects coding conventions |
| `RepoSnapshotCollector` | Collects repository state for analysis |
| `ProviderRegistry` | Registers and resolves providers |
| `StorageReaderRegistry` | Registers storage backends |
| `YamlSkillLoader` | Loads skill definitions from YAML |
| `ContextValidator` | Validates context files |
| `FileDecisionLogger` | Logs decisions to file |

### Language Detectors

| Detector | Languages |
|----------|-----------|
| `DotNetLanguageDetector` | C#, F# (.NET) |
| `PythonLanguageDetector` | Python |
| `TypeScriptLanguageDetector` | TypeScript, JavaScript |

---

## Infrastructure

**Project:** `AgentSmith.Infrastructure` | **Dependencies:** Contracts, Infrastructure.Core, external SDKs

Implements all provider interfaces using external libraries and APIs.

### AI Providers

| Provider | SDK | Models |
|----------|-----|--------|
| `ClaudeAgentProvider` | Anthropic.SDK | Claude Sonnet, Opus, Haiku |
| `OpenAiAgentProvider` | OpenAI SDK | GPT-4, GPT-4.1 |
| `GeminiAgentProvider` | Google AI SDK | Gemini 2.5 |
| `OllamaAgentProvider` | HTTP client | Any Ollama model |

Each provider has its own agentic loop implementation (`AgenticLoop`, `OpenAiAgenticLoop`, `GeminiAgenticLoop`, `OllamaAgenticLoop`) that handles tool calling, context management, and retry logic.

### Supporting AI Services

| Service | Purpose |
|---------|---------|
| `AgentPromptBuilder` | Constructs system/user prompts |
| `ClaudeContextCompactor` | Compresses conversation context when token limit is near |
| `ScoutAgent` | Lightweight codebase discovery (file listing, search) |
| `FileReadTracker` | Deduplicates file reads across turns |
| `TokenUsageTracker` | Tracks token consumption per request |
| `CostTracker` | Calculates cost from token usage |
| `ConfigBasedModelRegistry` | Resolves models from configuration |
| `PlanParser` | Parses LLM output into structured plans |

### Source Providers

| Provider | Backend |
|----------|---------|
| `GitHubSourceProvider` | Octokit (clone, branch, push, PR) |
| `AzureReposSourceProvider` | Azure DevOps SDK |
| `GitLabSourceProvider` | GitLab REST API |
| `LocalSourceProvider` | Local filesystem |

### Ticket Providers

| Provider | Backend |
|----------|---------|
| `GitHubTicketProvider` | Octokit |
| `AzureDevOpsTicketProvider` | Azure DevOps SDK |
| `GitLabTicketProvider` | GitLab REST API |
| `JiraTicketProvider` | Jira REST v3 |

### PR Diff Providers

| Provider | Purpose |
|----------|---------|
| `GitHubPrDiffProvider` | Fetch PR diffs from GitHub |
| `AzureDevOpsPrDiffProvider` | Fetch PR diffs from Azure DevOps |
| `GitLabPrDiffProvider` | Fetch MR diffs from GitLab |

### Output Strategies

| Strategy | Format |
|----------|--------|
| `ConsoleOutputStrategy` | Human-readable terminal output |
| `SarifOutputStrategy` | SARIF (Static Analysis Results Interchange Format) |
| `MarkdownOutputStrategy` | Rich Markdown report |
| `SummaryOutputStrategy` | Compact one-page summary |

### Tool Runners

| Runner | Backend |
|--------|---------|
| `DockerToolRunner` | Docker CLI for tool containers |
| `ProcessToolRunner` | Direct process execution |

### Other Infrastructure

| Service | Purpose |
|---------|---------|
| `RedisMessageBus` | Redis pub/sub for progress messages |
| `RedisProgressReporter` | Publishes pipeline progress to Redis |
| `DockerContainerRunner` | Runs containers for Nuclei/Spectral |
| `NucleiSpawner` | Spawns Nuclei security scanner |
| `SwaggerProvider` | Loads and preprocesses OpenAPI specs |
| `AgentProviderFactory` | Creates AI provider instances |
| `LlmClientFactory` | Creates LLM clients per project |
| `SourceProviderFactory` | Creates source providers |
| `TicketProviderFactory` | Creates ticket providers |

---

## Host

**Project:** `AgentSmith.Cli` | **Dependencies:** All layers (DI wiring)

The CLI entry point and webhook server. Wires all dependencies, defines CLI commands, routes webhooks.

### CLI Commands

| Command | Verb | Description |
|---------|------|-------------|
| `FixCommand` | `fix` | Fix a bug from a ticket |
| `FeatureCommand` | `feature` | Add a feature from a ticket |
| `SecurityScanCommand` | `security-scan` | Run security analysis |
| `ApiScanCommand` | `api-scan` | Run API security scan |
| `LegalCommand` | `legal` | Run legal document analysis |
| `MadCommand` | `mad` | Run MAD discussion |
| `InitCommand` | `init` | Initialize `.agentsmith/` in a project |
| `RunCommand` | `run` | Generic pipeline execution |
| `ServerCommand` | `server` | Start webhook listener |

### Webhook Handlers

| Handler | Event |
|---------|-------|
| `GitHubIssueWebhookHandler` | `issues.labeled` |
| `GitHubPrLabelWebhookHandler` | `pull_request.labeled` |
| `GitLabMrLabelWebhookHandler` | GitLab MR label events |
| `AzureDevOpsWorkItemWebhookHandler` | Azure DevOps work item updates |
| `WebhookSignatureValidator` | Validates webhook signatures (HMAC) |

### Other

| Service | Purpose |
|---------|---------|
| `ConfigDiscovery` | Implements the 4-step config file discovery |
| `ServiceProviderFactory` | Builds the DI container |
| `WebhookListener` | ASP.NET Core minimal API for webhook endpoints |

---

## Dispatcher

**Project:** `AgentSmith.Server` | **Dependencies:** Own contracts, Redis, platform SDKs

A separate ASP.NET Core process that bridges chat platforms to Agent Smith via ephemeral containers.

### Contracts

| Interface | Purpose |
|-----------|---------|
| `IJobSpawner` | Creates K8s Jobs or Docker containers |
| `IPlatformAdapter` | Abstracts chat platform (Slack, Teams) |
| `IMessageBus` | Redis pub/sub abstraction |
| `ILlmIntentParser` | LLM-based intent parsing |
| `IProjectResolver` | Maps project names to configuration |

### Services

| Service | Purpose |
|---------|---------|
| `IntentEngine` | Two-stage intent parsing (regex + LLM) |
| `ChatIntentParser` | Parses chat messages into structured intents |
| `KubernetesJobSpawner` | Creates K8s Jobs for agent execution |
| `DockerJobSpawner` | Creates Docker containers for agent execution |
| `MessageBusListener` | Listens for Redis messages and routes to adapters |
| `ConversationStateManager` | Tracks conversation context per channel/thread |
| `ClarificationStateManager` | Manages clarification flows |
| `ProjectResolver` | Resolves project names from configuration |
| `OrphanJobDetector` | Detects and cleans up stale jobs |
| `RedisMessageBus` | Redis pub/sub implementation |

### Slack Integration

| Service | Purpose |
|---------|---------|
| `SlackAdapter` | Receives Slack events, sends messages |
| `SlackInteractionHandler` | Handles modal submissions, button clicks |
| `SlackModalBuilder` | Builds Slack Block Kit modals |
| `SlackMessageDispatcher` | Sends formatted messages to channels |
| `SlackSignatureVerifier` | Validates Slack request signatures |
| `SlackErrorBlockBuilder` | Formats error messages as Slack blocks |
| `CachedTicketSearch` | Caches ticket search for autocomplete |
