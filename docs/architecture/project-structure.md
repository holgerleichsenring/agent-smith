# Project Structure

Full directory layout of the Agent Smith repository.

```
agent-smith/
├── .agentsmith/                    # Agent meta-files (project context)
│   ├── context.yaml                # Project description + phase tracking
│   ├── coding-principles.md        # Detected coding conventions
│   ├── phases/
│   │   ├── done/                   # Completed phase docs (historical)
│   │   ├── active/                 # Currently active phase (max 1)
│   │   └── planned/               # Upcoming phase requirements
│   └── runs/                       # Execution artifacts (r{NN}-slug/)
│
├── src/
│   ├── AgentSmith.Domain/          # Innermost layer — no dependencies
│   │   ├── Entities/               # Ticket, Repository, Plan, CodeChange, etc.
│   │   ├── Models/                 # Value objects: TicketId, BranchName, FilePath
│   │   └── Exceptions/            # AgentSmithException, ConfigurationException
│   │
│   ├── AgentSmith.Contracts/       # Interfaces + DTOs
│   │   ├── Commands/               # ICommandHandler, ICommandExecutor, PipelineContext
│   │   ├── Decisions/              # IDecisionLogger
│   │   ├── Models/                 # Finding, RunCostSummary, ParsedIntent, OutputContext
│   │   │   └── Configuration/     # AgentSmithConfig, ProjectConfig, ModelRegistryConfig
│   │   ├── Providers/              # IAgentProvider, IContainerRunner, IModelRegistry
│   │   └── Services/              # Service interfaces
│   │
│   ├── AgentSmith.Application/     # Use cases + handlers
│   │   ├── Models/                 # Context records per handler (39 types)
│   │   ├── Services/
│   │   │   ├── Handlers/          # Pipeline step handlers (39 handlers)
│   │   │   ├── Builders/          # Context builders for API/legal pipelines
│   │   │   └── Triggers/         # InboxPollingService
│   │   │   ├── ExecutePipelineUseCase.cs
│   │   │   ├── PipelineExecutor.cs
│   │   │   ├── CommandExecutor.cs
│   │   │   ├── CommandContextFactory.cs
│   │   │   ├── PipelineCostTracker.cs
│   │   │   ├── RegexIntentParser.cs
│   │   │   ├── LlmIntentParser.cs
│   │   │   └── TrackingLlmClient.cs
│   │   └── Extensions/            # DI registration, PipelineContext extensions
│   │
│   ├── AgentSmith.Infrastructure.Core/  # Shared infra (no external SDKs)
│   │   └── Services/
│   │       ├── Configuration/     # YamlConfigurationLoader, SecretsProvider
│   │       ├── Detection/         # Language detectors (.NET, Python, TypeScript)
│   │       ├── ProjectDetector.cs
│   │       ├── ContextGenerator.cs
│   │       ├── CodeMapGenerator.cs
│   │       ├── CodingPrinciplesGenerator.cs
│   │       ├── ProviderRegistry.cs
│   │       ├── RepoSnapshotCollector.cs
│   │       └── YamlSkillLoader.cs
│   │
│   ├── AgentSmith.Infrastructure/  # External integrations
│   │   ├── Models/                 # ToolDefinitions, BusMessage, ScoutResult
│   │   └── Services/
│   │       ├── Providers/
│   │       │   ├── Agent/         # Claude, OpenAI, Gemini, Ollama providers + loops
│   │       │   ├── Source/        # GitHub, AzureRepos, GitLab, Local source providers
│   │       │   └── Tickets/      # GitHub, AzureDevOps, GitLab, Jira ticket providers
│   │       ├── Factories/         # AgentProvider, LlmClient, Source, Ticket factories
│   │       ├── Output/            # Console, SARIF, Markdown, Summary strategies
│   │       ├── Tools/             # DockerToolRunner, ProcessToolRunner
│   │       ├── Bus/               # RedisMessageBus, RedisProgressReporter
│   │       ├── Containers/        # DockerContainerRunner
│   │       ├── Nuclei/            # NucleiSpawner
│   │       └── Spectral/         # SpectralSpawner
│   │
│   ├── AgentSmith.Cli/           # CLI + webhook entry point
│   │   ├── Commands/              # Fix, Feature, SecurityScan, ApiScan, Legal, etc.
│   │   ├── Services/
│   │   │   └── Webhooks/         # GitHub, GitLab, AzureDevOps webhook handlers
│   │   ├── Program.cs             # Entry point, System.CommandLine setup
│   │   ├── ConfigDiscovery.cs     # 4-step config file resolution
│   │   └── ServiceProviderFactory.cs  # DI container builder
│   │
│   └── AgentSmith.Server/     # Chat gateway (separate process)
│       ├── Contracts/              # IJobSpawner, IPlatformAdapter, IMessageBus
│       ├── Models/                 # ChatIntent, JobRequest, ConversationState
│       ├── Services/
│       │   ├── Adapters/          # SlackAdapter, modals, interactions
│       │   ├── Handlers/          # Dispatcher-specific handlers
│       │   ├── IntentEngine.cs
│       │   ├── KubernetesJobSpawner.cs
│       │   ├── DockerJobSpawner.cs
│       │   ├── MessageBusListener.cs
│       │   ├── ConversationStateManager.cs
│       │   ├── OrphanJobDetector.cs
│       │   └── ProjectResolver.cs
│       └── Program.cs             # Dispatcher entry point
│
├── tests/
│   └── AgentSmith.Tests/          # 567 tests (xUnit + Moq + FluentAssertions)
│
├── config/                         # Configuration templates
│   ├── prompts/                    # LLM prompt templates (.md files, loaded by FilePromptTemplateProvider)
│   └── skills/                     # Skill definitions, observation schema, security principles
├── k8s/                            # Kubernetes manifests
│   ├── base/                       # Kustomize base
│   │   ├── kustomization.yaml
│   │   ├── namespace.yaml
│   │   ├── configmap/
│   │   ├── dispatcher/
│   │   ├── rbac/
│   │   └── redis/
│   ├── overlays/
│   │   ├── dev/                   # Development overlay
│   │   └── prod/                  # Production overlay
│   └── secret-template.yaml       # Secret template (do not commit values)
│
├── docs/                           # MkDocs documentation site
│
├── deploy/                         # Deployment orchestration
│   ├── docker-compose.yml          # Full stack: agent, server, redis, ollama
│   ├── k8s/                        # Kubernetes manifests (base + overlays)
│   └── apply-k8s-secret.sh         # K8s secret setup script
│
├── AgentSmith.sln                  # Solution file
├── release-please-config.json      # Automated release configuration
└── version.txt                     # Current version
```

## Solution File

The `AgentSmith.sln` contains 8 projects:

| Project | Type | Description |
|---------|------|-------------|
| `AgentSmith.Domain` | Class Library | Entities, value objects, exceptions |
| `AgentSmith.Contracts` | Class Library | Interfaces, DTOs, config models |
| `AgentSmith.Application` | Class Library | Handlers, pipeline, use cases |
| `AgentSmith.Infrastructure.Core` | Class Library | Config, detection, registries |
| `AgentSmith.Infrastructure` | Class Library | AI, Git, tickets, output, tools |
| `AgentSmith.Cli` | Console App | CLI entry point + webhook server |
| `AgentSmith.Server` | Web App | Chat gateway + job spawner |
| `AgentSmith.Tests` | Test Project | 567 tests (xUnit) |

## Key Files

| File | Purpose |
|------|---------|
| `src/AgentSmith.Cli/Program.cs` | CLI command definitions (18 lines after refactor) |
| `src/AgentSmith.Application/Services/ExecutePipelineUseCase.cs` | Top-level orchestrator |
| `src/AgentSmith.Application/Services/PipelineExecutor.cs` | Runs ordered command sequence |
| `src/AgentSmith.Contracts/Commands/PipelinePresets.cs` | Pipeline step definitions |
| `src/AgentSmith.Cli/ConfigDiscovery.cs` | Config file resolution logic |
| `docker-entrypoint.sh` | Volume permission handling with gosu |
| `k8s/base/kustomization.yaml` | Kubernetes base configuration |
