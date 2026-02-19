# Agent Smith - Prompts Index

This directory is the single source of truth for architecture, planning, and coding standards.
Every design change is reflected here.

---

## Overview

| File | Content | Status |
|------|---------|--------|
| `architecture.md` | Overall architecture, business model, tech stack | Stable |
| `coding-principles.md` | Code quality rules (loaded by the agent at runtime) | Stable |
| `phase1-plan.md` | Phase 1 overview and dependencies | Done |
| `phase1-solution-structure.md` | Step 1: Create .NET solution | Done |
| `phase1-domain.md` | Step 2: Domain entities & value objects | Done |
| `phase1-contracts.md` | Step 3: MediatR-style command pattern & interfaces | Done |
| `phase1-config.md` | Step 4: YAML config loader | Done |
| `phase2-plan.md` | Phase 2 overview: Commands + Executor | Done |
| `phase2-executor.md` | CommandExecutor implementation | Done |
| `phase2-contexts.md` | All 9 command context records | Done |
| `phase2-handlers.md` | All 9 command handler stubs + DI registration | Done |
| `phase3-plan.md` | Phase 3 overview: Providers + Factories | Done |
| `phase3-factories.md` | Provider factories (Ticket, Source, Agent) | Done |
| `phase3-tickets.md` | AzureDevOps + GitHub ticket providers | Done |
| `phase3-source.md` | Local + GitHub source providers | Done |
| `phase3-agent.md` | Claude agent provider (Plan + Execution) | Done |
| `phase3-agentic-loop.md` | Agentic loop detail (Tools, Loop, Security) | Done |
| `phase4-plan.md` | Phase 4 overview: Pipeline execution | Done |
| `phase4-intent-parser.md` | RegexIntentParser (User input -> Intent) | Done |
| `phase4-pipeline-executor.md` | PipelineExecutor + CommandContextFactory | Done |
| `phase4-use-case.md` | ProcessTicketUseCase (Orchestration) | Done |
| `phase4-di-wiring.md` | DI registration + Host Program.cs | Done |
| `phase5-plan.md` | Phase 5 overview: CLI & Docker | Done |
| `phase5-cli.md` | System.CommandLine CLI (--help, --config, --dry-run) | Done |
| `phase5-docker.md` | Multi-stage Dockerfile + Docker Compose | Done |
| `phase5-smoke-test.md` | DI integration test + CLI smoke test | Done |
| `phase6-plan.md` | Phase 6 overview: Resilience + Retry | Done |
| `phase6-retry.md` | RetryConfig, ResilientHttpClientFactory, Polly | Done |
| `phase7-plan.md` | Phase 7 overview: Prompt Caching | Done |
| `phase7-caching.md` | CacheConfig, TokenUsageTracker, TokenUsageSummary | Done |
| `phase7-token-tracking.md` | Prompt caching activation, system prompt optimization | Done |
| `phase8-plan.md` | Phase 8 overview: Context Compaction | Done |
| `phase8-compaction.md` | CompactionConfig, IContextCompactor, ClaudeContextCompactor | Done |
| `phase8-file-tracking.md` | FileReadTracker, file read deduplication | Done |
| `phase9-plan.md` | Phase 9 overview: Model Registry & Scout | Done |
| `phase9-model-registry.md` | ModelRegistryConfig, IModelRegistry, TaskType | Done |
| `phase9-scout.md` | ScoutAgent, ScoutResult, ScoutTools | Done |
| `phase10-plan.md` | Phase 10 overview: Container production-ready | Done |
| `phase10-headless.md` | Headless mode (--headless, auto-approve) | Done |
| `phase10-docker.md` | Docker hardening (health check, non-root, .env) | Done |
| `phase11-plan.md` | Phase 11 overview: Multi-provider (OpenAI + Gemini) | Done |
| `phase11-openai.md` | OpenAI agent provider (GPT-4.1, tool calling loop) | Done |
| `phase11-gemini.md` | Gemini agent provider (2.5 Flash/Pro, function calling) | Done |
| `phase11-factory.md` | AgentProviderFactory extension for new providers | Done |
| `phase12-plan.md` | Phase 12 overview: Cost tracking | Done |
| `phase12-cost-tracker.md` | CostTracker, RunCostSummary, PricingConfig | Done |
| `phase12-phase-tracking.md` | Per-phase token tracking in TokenUsageTracker | Done |
| `phase13-plan.md` | Phase 13 overview: Ticket writeback & status | Done |
| `phase13-ticket-writeback.md` | ITicketProvider extensions (UpdateStatus, Close) | Done |
| `phase13-pipeline-integration.md` | PipelineExecutor + CommitAndPRHandler integration | Done |
| `phase14-plan.md` | Phase 14 overview: GitHub Action / Webhook trigger | Done |
| `phase14-github-action.md` | GitHub Actions workflow (issues.labeled trigger) | Done |
| `phase14-webhook.md` | WebhookListener (HttpListener, --server mode) | Done |
| `phase15-plan.md` | Phase 15 overview: Azure Repos source provider | Done |
| `phase15-azure-repos.md` | AzureReposSourceProvider (clone, branch, PR via DevOps API) | Done |
| `phase16-plan.md` | Phase 16 overview: Jira ticket provider | Done |
| `phase16-jira.md` | JiraTicketProvider (REST API v3, ADF parsing) | Done |
| `phase17-plan.md` | Phase 17 overview: GitLab provider (Source + Tickets) | Done |
| `phase17-gitlab-tickets.md` | GitLabTicketProvider (REST API v4) | Done |
| `phase17-gitlab-source.md` | GitLabSourceProvider (clone, branch, merge request) | Done |
| `phase18-plan.md` | Phase 18 overview: Multi-User Chat Gateway (Redis Streams + K8s Jobs) | In Progress |
| `phase18-redis-bus.md` | IMessageBus, RedisMessageBus, BusMessage protocol | Done |
| `phase18-progress-reporter.md` | IProgressReporter, ConsoleProgressReporter, RedisProgressReporter | Done |
| `phase18-dispatcher.md` | Dispatcher Service: Program.cs, ChatIntentParser, JobSpawner, MessageBusListener | Done |
| `phase18-conversation-state.md` | ConversationStateManager, ConversationState, Redis key schema | Done |
| `phase18-slack-adapter.md` | SlackAdapter, IPlatformAdapter, SlackAdapterOptions | Done |
| `phase19-plan.md` | Phase 19 overview: K8s Manifests + Dispatcher Dockerfile | Done |
| `phase19-dispatcher-dockerfile.md` | Dispatcher Dockerfile (multi-stage, ASP.NET Core, non-root) | Done |
| `phase19-redis.md` | Redis Deployment + Service (no PV, maxmemory 256mb, ephemeral) | Done |
| `phase19-secrets.md` | K8s Secret schema + secret-template.yaml | Done |
| `phase19-configmap.md` | ConfigMap for agentsmith.yml (generated from local config) | Done |
| `phase19-dispatcher-deployment.md` | Dispatcher Deployment + Service + liveness/readiness probes | Done |
| `phase19-rbac.md` | ServiceAccount + Role + RoleBinding for Job spawning | Done |
| `phase19-kustomize.md` | Kustomize base + overlays/dev + overlays/prod | Done |

---

## Phase Overview

- **Phase 1: Core Infrastructure** - Done
- **Phase 2: Free Commands (Stubs)** - Done
- **Phase 3: Providers** - Done
- **Phase 4: Pipeline Execution** - Done
- **Phase 5: CLI & Docker** - Done
- **Phase 6: Resilience - Retry** - Done
- **Phase 7: Prompt Caching** - Done
- **Phase 8: Context Compaction** - Done
- **Phase 9: Model Registry & Scout** - Done
- **Phase 10: Container Production-Ready** - Done
- **Phase 11: Multi-Provider (OpenAI + Gemini)** - Done
- **Phase 12: Cost Tracking** - Done
- **Phase 13: Ticket Writeback & Status** - Done
- **Phase 14: GitHub Action / Webhook Trigger** - Done
- **Phase 15: Azure Repos Source Provider** - Done
- **Phase 16: Jira Ticket Provider** - Done
- **Phase 17: GitLab Provider (Source + Tickets)** - Done
- **Phase 18: Multi-User Chat Gateway** - Done
- **Phase 19: K8s Manifests + Dispatcher Dockerfile** - Done

---

## Conventions

- All documentation and prompts are written in English.
- All code text (comments, exceptions, logs) is in English.
- Each phase has a `phase{N}-plan.md` as its entry point.
- Individual steps are split into `phase{N}-{topic}.md` files.
- Design changes -> update `architecture.md`.
- Code rule changes -> update `coding-principles.md`.
