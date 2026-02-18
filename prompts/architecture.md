# AGENT SMITH - ARCHITECTURE PROMPT

## Project Vision
Self-hosted AI coding agent. Takes ticket references, generates code changes via Claude/OpenAI, creates PRs. Open Source core + Pro features. Docker-based distribution.

---

## Business Model

### Open Source (Free)
- All providers (GitHub, Azure DevOps, GitLab, Jira)
- Basic workflow: `"fix #123 in project"` → Code → PR
- Self-hosted via Docker
- CLI interface

### Pro (Paid - $49/month)
- Auto-ticket creation from description
- Smart documentation generation
- Multi-repo orchestration
- Chat integrations (WhatsApp/Teams/Slack)
- Learning mode (remembers code review feedback)
- License key activation

---

## Repository Strategy

**Public Repo:** `agent-smith` (Open Source)
- Contains Free features
- Community contributions
- Docker Hub: `agentsmith/agent-smith:latest`

**Private Repo:** `agent-smith-pro`
- Forked from public repo
- Merges upstream changes (Free → Pro)
- Never merges back (Pro → Free)
- Contains Pro-only features
- Docker Hub: `agentsmith/agent-smith-pro:latest`

**Sync Strategy:**
```bash
git remote add upstream github.com/holger/agent-smith
git fetch upstream
git merge upstream/main  # Pro gets Free updates
# Pro code never goes back
```

---

## Technology Stack
- .NET 8 (C#)
- Anthropic C# SDK / OpenAI SDK
- Azure DevOps SDK / Octokit (GitHub) / LibGit2Sharp
- YamlDotNet (config)
- Docker
- xUnit (testing)

---

## Architecture Principles

### Code Quality (NON-NEGOTIABLE)
- Max 20 lines per method
- Max 120 lines per class
- One type per file
- SOLID principles
- DRY (Don't Repeat Yourself)
- Tell, Don't Ask pattern
- No public fields (properties only)
- Immutable value objects where possible

### Naming Conventions
- PascalCase for classes, methods, properties
- camelCase for parameters, local variables
- Interfaces prefixed with `I`
- Async methods suffixed with `Async`
- Private fields prefixed with `_`

### Architecture Style
- DDD (Domain-Driven Design)
- Clean Architecture
- MediatR-Style Command/Handler Pattern for pipeline execution
- Factory Pattern for provider instantiation
- Strategy Pattern for multi-provider support

---

## Project Structure

```
AgentSmith.sln
├── src/
│   ├── AgentSmith.Domain/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   └── Exceptions/
│   ├── AgentSmith.Contracts/
│   │   ├── Commands/
│   │   ├── Providers/
│   │   └── Services/
│   ├── AgentSmith.Application/
│   │   ├── Commands/
│   │   ├── Services/
│   │   └── UseCases/
│   ├── AgentSmith.Infrastructure/
│   │   ├── Providers/
│   │   │   ├── Tickets/
│   │   │   ├── Source/
│   │   │   └── Agent/
│   │   ├── Configuration/
│   │   └── Factories/
│   └── AgentSmith.Host/
│       ├── CLI/
│       └── Docker/
├── config/
│   ├── agentsmith.yml
│   └── coding-principles.md
└── tests/
    └── AgentSmith.Tests/
```

**Pro-Specific Additions (in private repo only):**
```
├── AgentSmith.Pro/
│   ├── Commands/
│   │   ├── AutoTicketCreationCommand.cs
│   │   ├── SmartDocumentationCommand.cs
│   │   └── MultiRepoOrchestratorCommand.cs
│   └── Services/
│       ├── LicenseValidator.cs
│       └── LearningService.cs
```

---

## Core Pattern: Command Pipeline (MediatR-Style)

### Pattern
Inspiriert von MediatR Request/Response. Strikte Trennung von Command (Was) und Handler (Wie).
Jeder Command definiert seinen eigenen Context-Typ. Der CommandExecutor löst den Handler per DI auf.

### Key Interfaces

**ICommandContext (Marker):**
- Marker interface für alle Command-Kontexte
- Jeder Command definiert seinen eigenen Context als Record
- Enthält die spezifischen Input-Daten für genau diesen Command

**ICommandHandler\<TContext\>:**
- Generic handler: `ICommandHandler<TContext> where TContext : ICommandContext`
- Single method: `ExecuteAsync(TContext context, CancellationToken)`
- Returns: `CommandResult`
- Jeder Handler implementiert genau eine Kombination
- Depends on providers via constructor injection

**CommandExecutor:**
- Löst `ICommandHandler<TContext>` per DI auf
- `ExecuteAsync<TContext>(TContext context, CancellationToken)` → findet Handler, ruft aus
- Zentrale Stelle für Cross-Cutting Concerns (Logging, Error Handling)

**PipelineContext:**
- Dictionary-based shared state bag zwischen Pipeline-Schritten
- Methods: `Get<T>(key)`, `Set<T>(key, value)`, `TryGet<T>(key, out value)`
- Wird in die einzelnen ICommandContext-Records injiziert wo nötig

**IProvider (base for all providers):**
- `ITicketProvider` - fetch ticket details
- `ISourceProvider` - git operations
- `IAgentProvider` - Claude/OpenAI interactions

### Beispiel

```csharp
// Command Context (Was soll passieren?)
public sealed record FetchTicketContext(
    TicketId TicketId,
    TicketConfig Config,
    PipelineContext Pipeline) : ICommandContext;

// Handler (Wie wird es gemacht?)
public sealed class FetchTicketHandler : ICommandHandler<FetchTicketContext>
{
    private readonly ITicketProviderFactory _factory;
    
    public async Task<CommandResult> ExecuteAsync(
        FetchTicketContext context, CancellationToken ct)
    {
        var provider = _factory.Create(context.Config);
        var ticket = await provider.GetTicketAsync(context.TicketId, ct);
        context.Pipeline.Set(ContextKeys.Ticket, ticket);
        return CommandResult.Ok($"Ticket {ticket.Id} fetched");
    }
}

// Auflösung per DI
services.AddTransient<ICommandHandler<FetchTicketContext>, FetchTicketHandler>();

// Aufruf
var result = await commandExecutor.ExecuteAsync(
    new FetchTicketContext(ticketId, config, pipeline), ct);
```

---

## Provider Authentication

### GitHub
- **Personal Access Token** (classic or fine-grained)
- Environment: `GITHUB_TOKEN`
- Scopes: `repo`, `workflow`, `write:packages`

### Azure DevOps
- **Personal Access Token (PAT)**
- Environment: `AZURE_DEVOPS_TOKEN`
- Scopes: `Code (Read/Write)`, `Work Items (Read/Write)`

### GitLab
- **Personal Access Token** or **Project Access Token**
- Environment: `GITLAB_TOKEN`
- Scopes: `api`, `write_repository`

### Jira
- **API Token** (Cloud) or **PAT** (Server/Data Center)
- Environment: `JIRA_TOKEN`, `JIRA_EMAIL`

### Git Operations (SSH)
- **SSH Key** mounted in Docker
- Mount: `-v ~/.ssh:/root/.ssh:ro`
- For: GitHub/GitLab/Azure Repos git operations

### Docker Run Example
```bash
docker run \
  -e GITHUB_TOKEN=ghp_xxx \
  -e ANTHROPIC_API_KEY=sk-xxx \
  -e AZURE_DEVOPS_TOKEN=xxx \
  -v ~/.ssh:/root/.ssh:ro \
  -v $(pwd):/workspace \
  agentsmith/agent-smith "fix #123 in project"
```

---

## Configuration-Driven Pipeline

### Config Structure (agentsmith.yml)

```yaml
projects:
  payslip:
    source:
      type: GitHub
      url: https://github.com/user/payslip
      auth: token  # or ssh
    tickets:
      type: AzureDevOps
      organization: myorg
      project: PayslipProject
      auth: token
    agent:
      type: Claude
      model: sonnet-4
    pipeline: fix-bug
    coding_principles_path: ./config/coding-principles.md
    
  review:
    source:
      type: Local
      path: /workspace/review
    tickets:
      type: Jira
      url: https://mycompany.atlassian.net
      project: REV
      auth: token
    agent:
      type: Claude
      model: sonnet-4
    pipeline: add-feature
    coding_principles_path: ./config/coding-principles.md

pipelines:
  fix-bug:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - LoadCodingPrinciplesCommand
      - AnalyzeCodeCommand
      - GeneratePlanCommand
      - ApprovalCommand
      - AgenticExecuteCommand
      - TestCommand
      - CommitAndPRCommand
      
  add-feature:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - LoadCodingPrinciplesCommand
      - GeneratePlanCommand
      - ApprovalCommand
      - AgenticExecuteCommand
      - GenerateTestsCommand
      - TestCommand
      - GenerateDocsCommand
      - CommitAndPRCommand

secrets:
  azure_devops_token: ${AZURE_DEVOPS_TOKEN}
  github_token: ${GITHUB_TOKEN}
  anthropic_api_key: ${ANTHROPIC_API_KEY}
  openai_api_key: ${OPENAI_API_KEY}
  jira_token: ${JIRA_TOKEN}
  jira_email: ${JIRA_EMAIL}
```

### Pipeline Execution Flow

1. User input: `"fix #34237 in payslip"`
2. Intent parser (Claude) extracts: ticket_id, project
3. Config lookup: find project settings + pipeline template
4. Build command name list from pipeline config
5. PipelineExecutor creates PipelineContext (shared state bag)
6. For each command name: build the matching ICommandContext, resolve ICommandHandler via CommandExecutor
7. Execute sequentially, stop on first failure

---

## Command Responsibilities

Each command = one `ICommandContext` record + one `ICommandHandler<TContext>`.
The context carries the specific inputs, the handler does the work.

### Free Commands

**FetchTicket:**
- Context: `FetchTicketContext(TicketId, TicketConfig, PipelineContext)`
- Handler: resolves provider via factory, fetches ticket
- Writes: `Ticket` to PipelineContext

**CheckoutSource:**
- Context: `CheckoutSourceContext(SourceConfig, BranchName, PipelineContext)`
- Handler: resolves provider (Local/GitHub/GitLab), clones or accesses repo
- Writes: `Repository` to PipelineContext

**LoadCodingPrinciples:**
- Context: `LoadCodingPrinciplesContext(string FilePath, PipelineContext)`
- Handler: reads markdown file from disk
- Writes: principles string to PipelineContext

**AnalyzeCode:**
- Context: `AnalyzeCodeContext(Repository, PipelineContext)`
- Handler: scans file structure, dependencies
- Writes: `CodeAnalysis` to PipelineContext

**GeneratePlan:**
- Context: `GeneratePlanContext(Ticket, CodeAnalysis, string CodingPrinciples, AgentConfig, PipelineContext)`
- Handler: calls AgentProvider
- Writes: `Plan` to PipelineContext

**Approval:**
- Context: `ApprovalContext(Plan, PipelineContext)`
- Handler: CLI prompt (y/n), if rejected → `CommandResult.Fail`
- Writes: approval boolean to PipelineContext

**AgenticExecute:**
- Context: `AgenticExecuteContext(Plan, Repository, string CodingPrinciples, AgentConfig, PipelineContext)`
- Handler: AgentProvider agentic loop (tool calling), reads/writes files iteratively
- Writes: `CodeChange` collection to PipelineContext

**Test:**
- Context: `TestContext(Repository, IReadOnlyList<CodeChange>, PipelineContext)`
- Handler: runs project-specific tests, if fail → `CommandResult.Fail`
- Writes: test results to PipelineContext

**CommitAndPR:**
- Context: `CommitAndPRContext(Repository, IReadOnlyList<CodeChange>, Ticket, SourceConfig, PipelineContext)`
- Handler: creates branch, commits, pushes, creates PR, links to ticket
- Writes: PR URL to PipelineContext

### Pro Commands (license-gated)

**AutoTicketCreation:**
- Context: `AutoTicketCreationContext(string Description, TicketConfig, PipelineContext)`
- Handler: creates ticket in Jira/ADO, continues with standard pipeline
- Requires: Pro license

**SmartDocumentation:**
- Context: `SmartDocumentationContext(Repository, IReadOnlyList<CodeChange>, PipelineContext)`
- Handler: updates README, API docs, generates architecture diagrams
- Requires: Pro license

**MultiRepoOrchestrator:**
- Context: `MultiRepoOrchestratorContext(Plan, IReadOnlyList<SourceConfig>, PipelineContext)`
- Handler: identifies affected repos, coordinates parallel PRs
- Requires: Pro license

---

## License Management

### LicenseValidator (Pro only)

**Methods:**
- `ValidateAsync(key)` → calls license server API
- `HasFeature(featureName)` → checks tier
- Caches validation result (24h)

**Usage in Commands:**
```csharp
if (!_license.HasFeature("auto-ticket"))
    return CommandResult.Fail("Pro license required. Visit agent-smith.dev/pro");
```

**Environment:**
```bash
docker run -e LICENSE_KEY=xxx-xxx agentsmith/agent-smith-pro "create feature X"
```

---

## Provider Factories

**Pattern:**
- Factory resolves provider type from config
- Returns interface implementation
- Example: `TicketProviderFactory.Create(config.Tickets.Type)`

**Supported Providers:**

**Tickets:**
- AzureDevOpsTicketProvider
- JiraTicketProvider
- GitHubTicketProvider (uses Issues)

**Source:**
- LocalSourceProvider (file path)
- GitHubSourceProvider (Octokit)
- GitLabSourceProvider
- AzureReposSourceProvider

**Agent:**
- ClaudeAgentProvider (Anthropic SDK, agentic loop)
- OpenAIAgentProvider (OpenAI SDK, agentic loop)

---

## Agentic Loop (Most Complex Part)

**AgentProvider executes plan via tool calling:**

**Tools available to agent:**
- `read_file` - read repo file
- `write_file` - modify repo file
- `list_files` - explore structure
- `run_command` - execute shell (tests, build)

**Loop:**
1. Send plan + coding principles to Claude/GPT
2. Agent responds with tool calls
3. Execute tools locally
4. Send results back
5. Repeat until agent says "done" or max iterations
6. Return all CodeChange objects

**Key:** Agent decides what files to change, in what order. Not hardcoded.

---

## Dependency Injection

**Service Registration:**
- Config: Singleton
- CommandHandlers: Transient (`ICommandHandler<TContext>` → concrete handler)
- CommandExecutor: Singleton (resolves handlers from `IServiceProvider`)
- Providers: Transient via factories
- UseCases: Transient
- Factories: Singleton
- Logging: AddConsole

**Handler Registration Example:**
```csharp
services.AddTransient<ICommandHandler<FetchTicketContext>, FetchTicketHandler>();
services.AddTransient<ICommandHandler<CheckoutSourceContext>, CheckoutSourceHandler>();
services.AddTransient<ICommandHandler<ApprovalContext>, ApprovalHandler>();
// ... one line per command
services.AddSingleton<ICommandExecutor, CommandExecutor>();
```

**No manual `new` calls. Everything resolved via DI.**

---

## Testing Strategy

**Unit Tests:**
- Commands with mocked providers
- Domain logic
- Value objects

**Integration Tests:**
- End-to-end with real Docker container
- Mock external APIs (GitHub, ADO, Anthropic)

**Coverage Target:** >80%

---

## Docker Strategy

**Dockerfile:**
- Multi-stage build (.NET SDK → runtime)
- Copy config templates
- Expose no ports (CLI tool)
- Entry point: CLI executable

**Distribution:**
- Free: `docker.io/agentsmith/agent-smith:latest`
- Pro: `docker.io/agentsmith/agent-smith-pro:latest`
- Or: Single image with license-based feature flags

**Usage:**
```bash
docker run \
  -e LICENSE_KEY=xxx \
  -e GITHUB_TOKEN=yyy \
  -e ANTHROPIC_API_KEY=zzz \
  -v ~/.ssh:/root/.ssh:ro \
  -v /local/repo:/workspace \
  agentsmith/agent-smith "fix #123 in project"
```

---

## Implementation Phases

### Phase 1: Core Infrastructure
- Solution structure
- Domain entities
- All contracts (interfaces only)
- Config loader

### Phase 2: Free Commands (Stubs)
- All Free commands with TODO bodies
- Proper signatures, logging, error handling

### Phase 3: Providers
- Start: AzureDevOpsTicketProvider
- Then: LocalSourceProvider
- Then: ClaudeAgentProvider (agentic loop)
- Test each before moving on

### Phase 4: Pipeline Execution
- RegexIntentParser (Regex-basiert, kein LLM-Call)
- CommandContextFactory (Command-Name → ICommandContext Mapping)
- PipelineExecutor (sequentielle Ausführung, Stop on Fail)
- ProcessTicketUseCase (Orchestrierung: Config → Intent → Pipeline)
- DI Wiring (Infrastructure + Application + Host Program.cs)

### Phase 5: CLI & Docker
- System.CommandLine CLI (--help, --config, --dry-run, --verbose)
- Multi-Stage Dockerfile (SDK → Runtime, ~150MB)
- Docker Compose Beispiel
- DI Integration Tests (alle Services auflösbar)

### Phase 6: Pro Features (Private Repo)
- Fork to agent-smith-pro
- Add Pro commands
- License validation
- Pro Docker image

---

## Critical Success Criteria

- User runs: `agentsmith "fix #123 in project"` → PR created
- Multi-file changes work
- Agentic loop iterates correctly
- All code follows 20/120 line limits
- Docker image <500MB
- License check works (Pro)
- Config-driven (no hardcoded providers)

---

## What NOT To Do

- No code in prompt (interfaces signatures only)
- No over-engineering (YAGNI)
- No premature optimization
- No tight coupling (always via interfaces)
- No God classes (split responsibilities)

---

## Key Risks & Mitigations

### Risk 1: Adoption Hurdle
**Problem:** Config + Prompts = friction
**Mitigation:** Starter templates, quick-start guide, demo video

### Risk 2: Prompt Quality
**Problem:** Bad prompts → bad code
**Mitigation:** Template library, Pro curated prompts, community sharing

### Risk 3: Agentic Loop Failures
**Problem:** 30-50% runs fail
**Mitigation:** Approval step, rollback, retry logic, transparency

### Risk 4: Data Privacy
**Problem:** Code goes to Anthropic/OpenAI
**Mitigation:** On-prem LLM support (Ollama), Azure OpenAI, AWS Bedrock

### Risk 5: License Enforcement
**Problem:** Pro code forkable
**Mitigation:** Convenience > piracy, complex Pro features, support value

### Risk 6: Maintenance Burden
**Problem:** Many provider combinations
**Mitigation:** Community providers, E2E tests, paid support for tested combos

---

## Future Enhancements (Post-MVP)

- **Telemetry:** Anonymous usage stats (opt-in)
- **Marketplace:** Community prompts, custom commands
- **IDE Integration:** VS Code extension
- **Observability:** Dashboard for runs, success rates, costs
- **On-prem LLMs:** Ollama, LocalAI support
- **Enterprise Features:** Multi-repo orchestration, learning mode, AI code review
