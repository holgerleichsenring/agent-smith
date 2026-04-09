# Architecture

Agent Smith is built on Clean Architecture principles with a strict dependency rule: inner layers never reference outer layers.

## Layer Diagram

```
┌─────────────────────────────────────────────────────────┐
│                      Dispatcher                         │
│         Slack/Teams gateway, job spawning,              │
│         intent routing, conversation state              │
├─────────────────────────────────────────────────────────┤
│                         Host                            │
│          CLI entry point, webhook server,               │
│          DI wiring, command routing                     │
├─────────────────────────────────────────────────────────┤
│                    Infrastructure                       │
│        AI providers (Claude, GPT-4, Gemini, Ollama),    │
│        Git (LibGit2Sharp), ticket providers,            │
│        Redis bus, output strategies, tool runners       │
├─────────────────────────────────────────────────────────┤
│                 Infrastructure.Core                     │
│        Config loading, project detection,               │
│        code map generation, provider registry           │
├─────────────────────────────────────────────────────────┤
│                     Application                         │
│        Pipeline executor, command handlers,             │
│        use cases, intent parsing, cost tracking         │
├─────────────────────────────────────────────────────────┤
│                      Contracts                          │
│        Interfaces, commands, DTOs,                      │
│        configuration models                             │
├─────────────────────────────────────────────────────────┤
│                       Domain                            │
│        Entities (Ticket, Plan, Repository),             │
│        value objects, exceptions                        │
└─────────────────────────────────────────────────────────┘
```

## Dependency Flow

```
Domain ← Contracts ← Application ← Infrastructure.Core ← Infrastructure ← Host
                                                                            ↑
                                                                       Dispatcher
```

- **Domain** has zero dependencies.
- **Contracts** depends only on Domain.
- **Application** depends on Contracts (and transitively Domain).
- **Infrastructure.Core** depends on Contracts for shared interfaces and config loading.
- **Infrastructure** implements Contracts interfaces using external SDKs.
- **Host** wires everything together via dependency injection.
- **Dispatcher** is a separate process with its own contracts and DI.

## Key Patterns

| Pattern | Where | Purpose |
|---------|-------|---------|
| Command/Handler | Application | Each pipeline step is a command with a handler |
| Pipeline | Application | Ordered sequence of commands per use case |
| Factory | Infrastructure | Create providers based on config (AI, Git, tickets) |
| Strategy | Infrastructure | Output formats (console, SARIF, markdown, summary) |
| Adapter | Dispatcher | Platform-specific chat integration (Slack, Teams) |
| Registry | Infrastructure.Core | Discover and register providers/detectors at startup |

## Pipelines

Agent Smith supports four pipeline types, each composed of different handler sequences:

| Pipeline | Steps | Trigger |
|----------|-------|---------|
| **fix-bug** | FetchTicket → CheckoutSource → BootstrapProject → LoadCodeMap → LoadDomainRules → LoadContext → AnalyzeCode → Triage → GeneratePlan → Approve → AgenticExecute → Test → WriteRunResult → CommitAndPR | CLI, Slack, webhook |
| **security-scan** | CheckoutSource → BootstrapProject → LoadDomainRules → AnalyzeCode → Triage → ConvergenceCheck → CompileDiscussion → DeliverOutput | CLI, Slack, webhook |
| **api-scan** | CheckoutSource → BootstrapProject → LoadSwagger → SpawnNuclei → SpawnSpectral → SecurityTriage → ApiSkillRound → CompileFindings → DeliverFindings | CLI, Slack, webhook |
| **legal-analysis** | AcquireSource → BootstrapDocument → LoadDomainRules → Triage → ConvergenceCheck → CompileDiscussion → DeliverOutput | CLI, Slack, inbox |
| **mad-discussion** | FetchTicket → CheckoutSource → BootstrapProject → LoadContext → Triage → ConvergenceCheck → CompileDiscussion → WriteRunResult → CommitAndPR | CLI, Slack |

## AI Provider Support

| Provider | Models | Use case |
|----------|--------|----------|
| Anthropic Claude | Sonnet, Opus, Haiku | Default for all tasks |
| OpenAI | GPT-4, GPT-4.1 | Alternative agent provider |
| Google Gemini | Gemini 2.5 | Alternative agent provider |
| Ollama | Any local model | Air-gapped / cost-sensitive |

The model registry allows per-task model assignment (e.g., Haiku for intent parsing, Opus for code generation).

## Further Reading

- [Layer Details](layers.md) — what each layer contains and why
- [Project Structure](project-structure.md) — full directory tree
- [Phase Workflow](phase-workflow.md) — how phases are planned, executed, and tracked
