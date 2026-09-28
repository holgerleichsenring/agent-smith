# Architecture

Agent Smith is built on Clean Architecture with a strict dependency rule: inner layers never reference outer layers. The backend is a .NET solution under `src/backend/`; the dashboard is a separate Next.js application under `src/dashboard/`.

## Layer diagram

```
┌─────────────────────────────────────────────────────────┐
│              Server              │         CLI          │
│   webhooks, pollers, queue,      │  one run, then exit; │
│   dashboard API + SignalR hub,   │  config from a file  │
│   chat adapters, sandboxes       │                      │
├─────────────────────────────────────────────────────────┤
│   Infrastructure                 │  Persistence         │
│   LLM clients, git and ticket    │  EF Core store:      │
│   providers, Redis, scanners,    │  SQLite, PostgreSQL, │
│   output strategies              │  MySQL, SQL Server   │
├─────────────────────────────────────────────────────────┤
│                 Infrastructure.Core                     │
│      config loading, secrets, skills, registries        │
├─────────────────────────────────────────────────────────┤
│                     Application                         │
│     pipeline executor, command handlers, use cases,     │
│     claim and queue, lifecycle services                 │
├─────────────────────────────────────────────────────────┤
│                      Contracts                          │
│     interfaces, commands, context keys, events,         │
│     configuration models                                │
├─────────────────────────────────────────────────────────┤
│                       Domain                            │
│     entities, value objects, exceptions                 │
└─────────────────────────────────────────────────────────┘

        Sandbox.Agent ── Sandbox.Wire ── Contracts
        (runs inside each sandbox; talks to the server over Redis)
```

## Dependency flow

```
Domain ← Contracts ← Application ─────────────────────────┐
             ↑                                            ├── Cli
             ├── Infrastructure.Core ← Infrastructure ────┤
             └── Infrastructure.Persistence(.SqlServer) ──┴── Server

Sandbox.Wire ← Contracts
Sandbox.Wire ← Sandbox.Agent
```

- **Domain** has no project references.
- **Contracts** references Domain and Sandbox.Wire, the step protocol shared with the sandbox agent.
- **Application** references Contracts and Domain only. It holds no external SDK.
- **Infrastructure.Core** references Contracts and implements what needs no external service: configuration loading, secrets, the skill catalog.
- **Infrastructure** references Infrastructure.Core and implements the Contracts interfaces with external SDKs.
- **Infrastructure.Persistence** references Contracts and holds the relational store. SQL Server migrations live in their own assembly.
- **Cli** and **Server** are the two hosts. Each wires the layers together through dependency injection.

## Key patterns

| Pattern | Where | Purpose |
|---------|-------|---------|
| Command/Handler | Application | Each pipeline step is a command with a handler |
| Pipeline preset | Contracts | Each pipeline is a fixed, ordered list of command names defined in code |
| Claim-then-enqueue | Application + Infrastructure | Single ingress for ticket-driven pipelines: webhook or poll → `TicketClaimService` → claim lock → status transition → `IRedisJobQueue` → `PipelineQueueConsumer`. See [Ticket lifecycle](../concepts/ticket-lifecycle.md) |
| Leader election | Application + Server | `LeaderElectedHostedService` over Redis leases, so only one replica polls and runs housekeeping |
| Sandbox over Redis | Server + Sandbox.Agent | File and command steps run in a per-repo sandbox that the server drives through Redis. See [Sandbox architecture](../concepts/sandbox-architecture.md) |
| Decorator chain | Infrastructure | Every LLM call goes through one `IChatClient` chain: rate limit, retry, events, compaction, tool loop. See [AI clients](ai-clients.md) |
| Strategy | Infrastructure | Output formats (console, summary, markdown, SARIF) |
| Adapter | Server | Chat platforms (Slack, Teams) and the dashboard behind one `IPlatformAdapter` |

## Pipelines

Agent Smith ships eight pipeline presets, each a fixed list of steps. Those that need judgement hand it to one master skill at their `AgenticMaster` step.

| Pipeline | Type | Master |
|----------|------|--------|
| `code` | hierarchical | `coding-agent-master` |
| `pr-review` | structured | `pr-review-master` |
| `security-scan` | structured | `security-master` |
| `api-security-scan` | structured | `api-security-master` |
| `legal-analysis` | discussion | `legal-analyst-master` |
| `mad-discussion` | discussion | `mad-discussion-master` |
| `init-project` | discussion | none |
| `spec-dialog` | discussion | `design-partner-master` |

The steps of each are on the [Pipelines](../pipelines/index.md) page, which is generated from the presets.

## AI providers

Agent types are `claude`, `openai`, `azure_openai`, `gemini`, `ollama` and `copilot`. Each role (primary, planning, scout, summarization and others) can run on its own model. See [AI clients](ai-clients.md) and [AI providers](../../connect-your-stuff/ai-providers.md).

## Further reading

- [Layer details](layers.md): what each project contains
- [Project structure](project-structure.md): the repository layout
- [Event schema policy](event-schema-policy.md): how the run-event contracts may change
- [Phase workflow](phase-workflow.md): how phases are planned, executed and tracked
