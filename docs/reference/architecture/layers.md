# Layer Details

Each project in `src/backend/` has one responsibility and a fixed set of references. This page names the types you meet first in each one; it is a map, not an inventory.

## Domain

**Project:** `AgentSmith.Domain` | **References:** none

The innermost layer: entities, value objects and exceptions, with no framework or package references.

| Kind | Examples |
|------|----------|
| Entities (`Entities/`) | `Ticket`, `TicketComment`, `Repository`, `Plan`, `PlanStep`, `PlanDecision`, `CodeChange`, `Diff`, `AttachmentRef` |
| Value objects and results (`Models/`) | `TicketId`, `BranchName`, `FilePath`, `ProjectName`, `CommandResult`, `PipelineCommand`, the run and phase accounts (`RunAccounts`, `PhaseAccounts`, `SpecAccount`), the scan contract (`ScanContract`, `ScanCriterion`), PR-review models (`PrDiffAnalysis`, `PrReviewInlineComment`) |
| Exceptions (`Exceptions/`) | `AgentSmithException` (base), `ConfigurationException`, `ProviderException`, `TicketNotFoundException`, `CapacityExhaustedException`, `StaleConfigVersionException`, `DataArchiveException` |

---

## Contracts

**Project:** `AgentSmith.Contracts` | **References:** Domain, Sandbox.Wire

Interfaces, commands, configuration models and the event records. Application and Infrastructure both depend on Contracts and never on each other.

| Area | Key types |
|------|-----------|
| Commands (`Commands/`) | `ICommandHandler<TContext>`, `ICommandExecutor`, `ICommandContext`, `PipelineContext`, `CommandNames`, `ContextKeys`, `PipelinePresets` (the step list of every pipeline) |
| LLM access | `IChatClientFactory` (see [AI clients](ai-clients.md)), `IModelRegistry` |
| Sandbox (`Sandbox/`) | `ISandbox`, `ISandboxFactory` |
| Events (`Events/`) | `IDomainEvent`, `IEventPublisher`, the run and system event records; see [Event schema policy](event-schema-policy.md) |
| Queue and configuration | `IRedisJobQueue`, `IConfigStore`, `AgentSmithConfig`, `AgentConfig` |
| Output and decisions | `IOutputStrategy`, `OutputContext`, `IDecisionLogger`, `IContainerRunner` |

---

## Application

**Project:** `AgentSmith.Application` | **References:** Contracts, Domain

The use-case layer: the pipeline executor, every step handler and the services around a run. No external SDK.

### Running a pipeline

| Service | Purpose |
|---------|---------|
| `ExecutePipelineUseCase` | Resolves config, project and pipeline, builds the context and runs it. The CLI calls it directly; the server calls it from the queue consumer |
| `PipelineExecutor` | Runs the ordered command list with lifecycle transitions |
| `CommandExecutor` | Dispatches one command to its handler |
| `CommandContextFactory` | Builds the typed context each handler receives |
| `PipelineCostTracker` | Aggregates token usage and cost across the run |

### Ingress and lifecycle

| Service | Purpose |
|---------|---------|
| `TicketClaimService` | Single ingress for ticket-driven pipelines: pre-checks, claim lock, status transition, enqueue |
| `PipelineQueueConsumer` | Pulls `PipelineRequest`s from `IRedisJobQueue` and runs them with bounded concurrency |
| `PollerHostedService` | Runs the configured `IEventPoller`s |
| `LeaderElectedHostedService` | Runs its work only while holding a named Redis lease |
| `EnqueuedReconciler` | Re-enqueues taken tickets that have no fresh run lease |
| `ActiveRunReaper` | Releases the lease of a run whose heartbeat went stale, so its ticket can be claimed again |

The path is: webhook handler or poller → `TicketClaimService` → `IRedisJobQueue` → `PipelineQueueConsumer` → `ExecutePipelineUseCase` → `PipelineExecutor`. See [Ticket lifecycle](../concepts/ticket-lifecycle.md) for the state machine.

### Handlers

`Services/Handlers/` holds one handler per pipeline step, each implementing `ICommandHandler<TContext>`. The ones that carry judgement are `AgenticMasterHandler`, which runs a pipeline's master skill, and `PhaseSequenceHandler`, which runs the `code` pipeline's per-phase loop. Which handlers a pipeline runs, and in what order, is on the [Pipelines](../pipelines/index.md) page.

---

## Infrastructure.Core

**Project:** `AgentSmith.Infrastructure.Core` | **References:** Contracts, Domain, SkillsPackaging

Infrastructure that needs no external service.

| Service | Purpose |
|---------|---------|
| `YamlConfigurationLoader` | Loads and validates `agentsmith.yml` |
| `DbConfigStore` | Serves the configuration from the database on a server |
| `SecretsProvider` | Resolves secrets from environment variables |
| `YamlSkillLoader` | Loads skill definitions from the catalog |
| `ProviderRegistry<T>`, `StorageReaderRegistry` | Register and resolve providers by type |
| `RepositoryDecisionLogger` | Records a decision on the run and copies it into the repository through its sandbox |

---

## Infrastructure

**Project:** `AgentSmith.Infrastructure` | **References:** Contracts, Domain, Infrastructure.Core, external SDKs

Implements the Contracts interfaces against external libraries and services.

| Area | Key types |
|------|-----------|
| LLM clients | `ChatClientFactory` and one `IChatClientBuilder` per provider, `ConfigBasedModelRegistry`. See [AI clients](ai-clients.md) |
| Source providers | `GitHubSourceProvider`, `AzureReposSourceProvider`, `GitLabSourceProvider`, `LocalSourceProvider` |
| Ticket providers | `GitHubTicketProvider`, `AzureDevOpsTicketProvider`, `GitLabTicketProvider`, `JiraTicketProvider` |
| PR diffs | `GitHubPrDiffProvider`, `AzureDevOpsPrDiffProvider`, `GitLabPrDiffProvider` |
| Redis | `RedisJobQueue`, `RedisEventPublisher` (run events into Redis Streams), `RedisMessageBus` |
| Sandbox | `InProcessSandbox`, the sandbox the CLI uses |
| API scanners | `NucleiSpawner`, `SpectralSpawner`, `ZapSpawner`, run through `DockerToolRunner` or `ProcessToolRunner` |
| Output strategies | `ConsoleOutputStrategy`, `SummaryOutputStrategy`, `MarkdownOutputStrategy`, `SarifOutputStrategy` |

---

## Infrastructure.Persistence

**Projects:** `AgentSmith.Infrastructure.Persistence`, `AgentSmith.Infrastructure.Persistence.SqlServer` | **References:** Contracts, Domain

The relational store behind run records, projections, the configuration and the data archive. `AgentSmithDbContext` is an EF Core context that runs on SQLite, PostgreSQL, MySQL or SQL Server, chosen by the `persistence:` block. SQL Server keeps its migrations in the separate `.SqlServer` assembly. Migrations are applied by `agent-smith database migrate`, never by the server on startup.

---

## CLI

**Project:** `AgentSmith.Cli` | **References:** Application, Infrastructure, Infrastructure.Persistence(.SqlServer)

The console tool: one run, then exit. It reads its whole configuration from `agentsmith.yml` and runs its sandbox in-process.

| Verb | Purpose |
|------|---------|
| `code` | Run the `code` pipeline for a ticket (`fix` and `feature` are deprecated aliases) |
| `security-scan`, `api-scan`, `security-trend` | Security scans and their trend |
| `mad`, `legal` | Discussion pipelines |
| `init` | Bootstrap `.agentsmith/` in a project's repositories |
| `doctor`, `demo` | Active preflight; a self-contained demo run |
| `config`, `database`, `archive` | Configuration import, export and validation; schema migrations; moving an installation's data between database providers |
| `skills pull`, `validate-concepts` | Download a skill catalog release; check skill `activates_when` expressions against the concept vocabulary |
| `compile-wiki`, `autonomous` | Compile run history into a knowledge-base wiki; observe a project and write improvement tickets |

Supporting types: `ConfigDiscovery` (where the config file is found), `ServiceProviderFactory` (the CLI's DI container), `ConsoleDialogueTransport` (dialogue questions on stdin/stdout). See [Host it: CLI](../../host-it/cli.md).

---

## Server

**Project:** `AgentSmith.Server` | **References:** Contracts, Application, Infrastructure, Infrastructure.Persistence(.SqlServer)

The long-running deployment: one ASP.NET Core process with one DI tree.

| Area | Key types and routes |
|------|---------------------|
| Webhooks | `POST /webhook` and `/webhook/{github,gitlab,jira}`; `WebhookRequestProcessor` detects the platform, verifies the signature and dispatches to one of the `IWebhookHandler`s |
| Dashboard API | `/api/...` endpoints and the SignalR hub `JobsHub` at `/hub/jobs` |
| Health | `GET /health`: liveness plus the startup preflight verdict |
| Hosted services | `QueueConsumerHostedService`, `PollerLeaderHostedService`, `HousekeepingLeaderHostedService`, `ActiveRunReaperHostedService`, `ConfigStoreReloadHostedService`, `SkillsCatalogReloadHostedService`, `RunRetentionHostedService`, `RepoDiscoveryRefreshHostedService` |
| Sandboxes | `DockerSandboxFactory`, `KubernetesSandboxFactory` |
| Jobs | `IJobSpawner` with `DockerJobSpawner` and `KubernetesJobSpawner` |
| Chat | `IPlatformAdapter` with `SlackAdapter`, `TeamsAdapter`, `DashboardAdapter`; `IntentEngine` and `ChatIntentParser` turn a message into an intent |

---

## Sandbox projects

**Projects:** `AgentSmith.Sandbox.Wire`, `AgentSmith.Sandbox.Agent`

`Sandbox.Wire` defines the step protocol (`Step`, `StepEvent`, `StepResult`, `RedisKeys`, `SizeLimits`) that the server and the agent share. `Sandbox.Agent` is the small executable injected into each sandbox: `JobLoop` takes steps from Redis, `StepExecutor` runs them, `HeartbeatLoop` reports liveness. See [Sandbox architecture](../concepts/sandbox-architecture.md) and [Sandbox agent](../concepts/sandbox-agent.md).

---

## SkillsPackaging

**Project:** `AgentSmith.SkillsPackaging`

A build-time check on the skill catalog that `Infrastructure.Core` embeds. Building `Infrastructure.Core` runs it on the pinned catalog tarball; `MasterDescriptionValidator` reports every master skill the loader would reject, and a violation fails the build.
