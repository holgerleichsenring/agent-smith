# Phase 3: Providers - Implementation Plan

## Goal
Real provider implementations + factories.
Handler stubs from Phase 2 are wired up with real provider calls.
After Phase 3: The pipeline can fetch real tickets, check out repos, and generate code.

---

## Prerequisite
- Phase 2 completed (all handler stubs, CommandExecutor working)

## Order (per Architecture)

The order is deliberately chosen - each provider can be tested in isolation.

### Step 1: Provider Factories
See: `prompts/phase3-factories.md`

Factories for all three provider types. Resolve the correct provider based on `config.Type`.
Project: `AgentSmith.Infrastructure/Factories/`

### Step 2: Ticket Providers
See: `prompts/phase3-tickets.md`

First provider: AzureDevOpsTicketProvider (with Azure DevOps SDK).
Then: GitHubTicketProvider (with Octokit).
Optional Phase 3: JiraTicketProvider.
Plus: FetchTicketHandler from stub to real implementation.
Project: `AgentSmith.Infrastructure/Providers/Tickets/`

### Step 3: Source Providers
See: `prompts/phase3-source.md`

LocalSourceProvider (file system + LibGit2Sharp).
Then: GitHubSourceProvider (Octokit for PRs, LibGit2Sharp for Git).
Plus: CheckoutSourceHandler + CommitAndPRHandler from stub to real.
Project: `AgentSmith.Infrastructure/Providers/Source/`

### Step 4: Agent Provider (Agentic Loop)
See: `prompts/phase3-agent.md`

ClaudeAgentProvider with Anthropic SDK. The core piece.
- Tool Definitions (read_file, write_file, list_files, run_command)
- Agentic Loop (send → tool calls → execute → send back → repeat)
- Plan Generation + Plan Execution
Plus: GeneratePlanHandler + AgenticExecuteHandler from stub to real.
Project: `AgentSmith.Infrastructure/Providers/Agent/`

### Step 5: Wire up remaining handlers
AnalyzeCodeHandler + TestHandler from stub to real.
These don't need providers, only file system/process calls.

### Step 6: Tests
- Unit tests for each factory
- Unit tests for each provider (with mocked HTTP clients)
- Integration tests for LocalSourceProvider (real file system)

### Step 7: Verify
```bash
dotnet build
dotnet test
```

---

## NuGet Packages (Phase 3)

| Project | Package | Purpose |
|---------|---------|---------|
| AgentSmith.Infrastructure | Anthropic.SDK | Claude API |
| AgentSmith.Infrastructure | Octokit | GitHub API |
| AgentSmith.Infrastructure | LibGit2Sharp | Git Operations |
| AgentSmith.Infrastructure | Microsoft.TeamFoundationServer.Client | Azure DevOps API |
| AgentSmith.Infrastructure | Microsoft.Extensions.Logging.Abstractions | ILogger<T> |

---

## Dependencies

```
Step 1 (Factories)
    ├── Step 2 (Tickets) ← needs TicketProviderFactory
    ├── Step 3 (Source) ← needs SourceProviderFactory
    └── Step 4 (Agent) ← needs AgentProviderFactory
         └── Step 5 (Remaining Handlers)
              └── Step 6 (Tests)
                   └── Step 7 (Verify)
```

Steps 2, 3, 4 can theoretically run in parallel, but sequential is safer due to SDK conflicts.

---

## Definition of Done (Phase 3)
- [ ] All three factory implementations present
- [ ] AzureDevOpsTicketProvider + GitHubTicketProvider implemented
- [ ] LocalSourceProvider + GitHubSourceProvider implemented
- [ ] ClaudeAgentProvider with Agentic Loop implemented
- [ ] All handler stubs replaced by real implementations
- [ ] Unit tests for factories + providers
- [ ] `dotnet build` + `dotnet test` pass without errors
- [ ] All files adhere to Coding Principles (20/120, English)
