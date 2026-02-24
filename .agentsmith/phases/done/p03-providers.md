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


---

# Phase 3 - Step 4: Agent Provider (Agentic Loop)

## Goal
Implement ClaudeAgentProvider with Anthropic SDK. The core piece of the system.
Project: `AgentSmith.Infrastructure/Providers/Agent/`

---

## Architecture

The Agent Provider has two responsibilities:
1. **Generate plan** (`GeneratePlanAsync`) - Single API call
2. **Execute plan** (`ExecutePlanAsync`) - Agentic loop with tool calling

The agentic loop is the most complex feature. The agent decides on its own which files
it reads, modifies, and in what order.

---

## ClaudeAgentProvider
```
File: src/AgentSmith.Infrastructure/Providers/Agent/ClaudeAgentProvider.cs
```

**NuGet:** `Anthropic.SDK`

**Constructor:**
- `string apiKey`
- `string model` (e.g. `"claude-sonnet-4-20250514"`)
- `ILogger<ClaudeAgentProvider> logger`

### GeneratePlanAsync

1. Build system prompt with Coding Principles
2. Build user prompt with ticket details + code analysis
3. Send to Claude API (no tool calling, text only)
4. Parse response → Domain `Plan`

**System Prompt Template:**
```
You are a senior software engineer. Analyze the following ticket and codebase,
then create a detailed implementation plan.

## Coding Principles
{codingPrinciples}

## Respond in JSON format:
{
  "summary": "...",
  "steps": [
    { "order": 1, "description": "...", "target_file": "...", "change_type": "Create|Modify|Delete" }
  ]
}
```

### ExecutePlanAsync (Agentic Loop)

This is the core. See `prompts/phase3-agentic-loop.md` for details.

---

## Handler Updates

**GeneratePlanHandler:** Stub → real
```csharp
var provider = factory.Create(context.AgentConfig);
var plan = await provider.GeneratePlanAsync(
    context.Ticket, context.CodeAnalysis, context.CodingPrinciples, cancellationToken);
context.Pipeline.Set(ContextKeys.Plan, plan);
```

**AgenticExecuteHandler:** Stub → real
```csharp
var provider = factory.Create(context.AgentConfig);
var changes = await provider.ExecutePlanAsync(
    context.Plan, context.Repository, context.CodingPrinciples, cancellationToken);
context.Pipeline.Set(ContextKeys.CodeChanges, changes);
```

---

## Tests

**ClaudeAgentProviderTests:**
- `GeneratePlanAsync_ValidInput_ReturnsPlan` (mocked HTTP client)
- `ExecutePlanAsync_WithToolCalls_ReturnsChanges` (mocked HTTP client)


---

# Phase 3 - Agentic Loop (Detail)

## Goal
Detailed description of the agentic loop in `ClaudeAgentProvider.ExecutePlanAsync`.
This is the most complex logic in the entire system.

---

## Flow

```
┌─────────────────────────────────────────────────────┐
│  1. Build initial message with plan + principles    │
│  2. Define tools (read_file, write_file, etc.)      │
│  3. Send to Claude API                              │
│                                                     │
│  ┌──── LOOP (max iterations) ──────────────────┐    │
│  │  4. Receive response                        │    │
│  │  5. If no tool_use → agent is done → break  │    │
│  │  6. For each tool_use block:                │    │
│  │     - Execute tool locally                  │    │
│  │     - Collect tool result                   │    │
│  │  7. Send tool results back to Claude        │    │
│  │  8. Track file changes                      │    │
│  └─────────────────────────────────────────────┘    │
│                                                     │
│  9. Return collected CodeChange objects              │
└─────────────────────────────────────────────────────┘
```

---

## Tool Definitions

### read_file
```json
{
  "name": "read_file",
  "description": "Read the contents of a file in the repository",
  "input_schema": {
    "type": "object",
    "properties": {
      "path": { "type": "string", "description": "Relative path from repo root" }
    },
    "required": ["path"]
  }
}
```
**Execution:** `File.ReadAllText(Path.Combine(repoRoot, path))`
**Error:** File not found → return error message as tool result

### write_file
```json
{
  "name": "write_file",
  "description": "Write or overwrite a file in the repository",
  "input_schema": {
    "type": "object",
    "properties": {
      "path": { "type": "string", "description": "Relative path from repo root" },
      "content": { "type": "string", "description": "Complete file content" }
    },
    "required": ["path", "content"]
  }
}
```
**Execution:** `File.WriteAllText(Path.Combine(repoRoot, path), content)`
**Side effect:** Track as `CodeChange(path, content, "Create"/"Modify")`

### list_files
```json
{
  "name": "list_files",
  "description": "List files in a directory",
  "input_schema": {
    "type": "object",
    "properties": {
      "path": { "type": "string", "description": "Relative directory path, empty for root" }
    },
    "required": ["path"]
  }
}
```
**Execution:** `Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories)`
**Return:** Newline-separated relative paths

### run_command
```json
{
  "name": "run_command",
  "description": "Run a shell command in the repository directory",
  "input_schema": {
    "type": "object",
    "properties": {
      "command": { "type": "string", "description": "Shell command to execute" }
    },
    "required": ["command"]
  }
}
```
**Execution:** `Process.Start("bash", "-c", command)` in the repo directory
**Return:** stdout + stderr, exit code
**Security:** Timeout (60s), no access outside the repo

---

## Tool Executor

```
File: src/AgentSmith.Infrastructure/Providers/Agent/ToolExecutor.cs
```

Central class that dispatches tool calls:

```csharp
public sealed class ToolExecutor(string repositoryPath, ILogger logger)
{
    private readonly List<CodeChange> _changes = new();

    public async Task<string> ExecuteAsync(string toolName, JsonElement input)
    {
        return toolName switch
        {
            "read_file" => await ReadFile(input),
            "write_file" => await WriteFile(input),
            "list_files" => ListFiles(input),
            "run_command" => await RunCommand(input),
            _ => $"Unknown tool: {toolName}"
        };
    }

    public IReadOnlyList<CodeChange> GetChanges() => _changes.AsReadOnly();
}
```

**Notes:**
- `_changes` tracks all write_file calls
- Path validation: No `..`, no absolute paths (security)
- run_command: Timeout, working directory = repo root
- Errors in tools → error message returned as string (no throw)

---

## Agentic Loop Implementation

```
File: src/AgentSmith.Infrastructure/Providers/Agent/AgenticLoop.cs
```

Separate class for the loop logic, decoupled from the provider.

**Constructor:**
- `AnthropicClient client`
- `string model`
- `ToolExecutor toolExecutor`
- `ILogger logger`
- `int maxIterations = 25`

**RunAsync(string systemPrompt, string userMessage):**
1. Create messages list: `[{role: "user", content: userMessage}]`
2. Loop:
   a. API call with messages + tools
   b. Append assistant response to messages
   c. If `stop_reason == "end_turn"` → break
   d. For each `tool_use` block:
      - Execute via ToolExecutor
      - Append `tool_result` to messages
   e. Iteration counter check
3. Return: `toolExecutor.GetChanges()`

**Important:**
- Messages list grows with each step (conversation history)
- `stop_reason == "tool_use"` → continue
- `stop_reason == "end_turn"` → agent is done
- Max iterations as safety net (default: 25)

---

## Directory Structure

```
src/AgentSmith.Infrastructure/Providers/Agent/
├── ClaudeAgentProvider.cs    ← IAgentProvider Implementation
├── AgenticLoop.cs            ← Loop logic
├── ToolExecutor.cs           ← Tool dispatch + execution
└── ToolDefinitions.cs        ← Tool JSON schemas as constants
```

---

## Security Notes

- **Path Traversal:** Validate all paths - no `..`, no absolute paths
- **Command Injection:** run_command has a timeout (60s) and runs in the repo directory
- **API Key:** Never log, never leak in responses
- **Max Iterations:** Prevents infinite loops (default: 25, configurable)
- **File Size:** Truncate large files (>100KB) on read_file


---

# Phase 3 - Step 1: Provider Factories

## Goal
Implement factories that instantiate the correct provider based on the `Type` field in the config.
Project: `AgentSmith.Infrastructure/Factories/`

---

## TicketProviderFactory
```
File: src/AgentSmith.Infrastructure/Factories/TicketProviderFactory.cs
```

```csharp
public sealed class TicketProviderFactory(IServiceProvider serviceProvider)
    : ITicketProviderFactory
{
    public ITicketProvider Create(TicketConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "azuredevops" => CreateAzureDevOps(config),
            "github" => CreateGitHub(config),
            "jira" => throw new NotSupportedException("Jira provider not yet implemented"),
            _ => throw new ConfigurationException($"Unknown ticket provider: {config.Type}")
        };
    }
}
```

**Behavior:**
- Switch on `config.Type` (case-insensitive)
- Instantiates the matching provider with the config values
- Secrets (token etc.) are read from environment variables
- Unknown type → `ConfigurationException`

---

## SourceProviderFactory
```
File: src/AgentSmith.Infrastructure/Factories/SourceProviderFactory.cs
```

**Behavior:**
- `"local"` → `LocalSourceProvider(config.Path)`
- `"github"` → `GitHubSourceProvider(config.Url, token)`
- `"gitlab"` → `throw new NotSupportedException(...)` (Phase 3 scope)
- `"azurerepos"` → `throw new NotSupportedException(...)` (Phase 3 scope)

---

## AgentProviderFactory
```
File: src/AgentSmith.Infrastructure/Factories/AgentProviderFactory.cs
```

**Behavior:**
- `"claude"` → `ClaudeAgentProvider(apiKey, config.Model)`
- `"openai"` → `throw new NotSupportedException(...)` (Phase 3 scope)

---

## Secrets Resolution

The factories read API keys from the DI container.
For this, a `SecretsProvider` class is registered that wraps environment variables.

```
File: src/AgentSmith.Infrastructure/Configuration/SecretsProvider.cs
```

```csharp
public sealed class SecretsProvider
{
    public string GetRequired(string envVarName)
    {
        return Environment.GetEnvironmentVariable(envVarName)
            ?? throw new ConfigurationException(
                $"Required environment variable '{envVarName}' is not set.");
    }

    public string? GetOptional(string envVarName)
    {
        return Environment.GetEnvironmentVariable(envVarName);
    }
}
```

---

## Notes
- Factories as `sealed` classes.
- `IServiceProvider` via constructor injection for access to logger, secrets, etc.
- Unimplemented providers throw `NotSupportedException` with a clear message.
- Factories are registered as singletons.


---

# Phase 3 - Step 3: Source Providers

## Goal
Real implementations for Git operations (checkout, commit, push, create PR).
Project: `AgentSmith.Infrastructure/Providers/Source/`

---

## LocalSourceProvider
```
File: src/AgentSmith.Infrastructure/Providers/Source/LocalSourceProvider.cs
```

**NuGet:** `LibGit2Sharp`

For local repositories that already exist on disk.

**Constructor:**
- `string basePath` (from config `source.path`)

**CheckoutAsync:**
1. Open repository with `new LibGit2Sharp.Repository(basePath)`
2. Create branch: `repo.CreateBranch(branch.Value)`
3. Checkout: `Commands.Checkout(repo, branch)`
4. Return Domain `Repository(basePath, branch, remote.Url)`

**CommitAndPushAsync:**
1. Stage all changes: `Commands.Stage(repo, "*")`
2. Commit: `repo.Commit(message, signature, signature)`
3. Push: `repo.Network.Push(remote, refspec)`

**CreatePullRequestAsync:**
- For Local Provider: Only log, no PR possible
- Return: `"Local repository - no PR created, branch pushed: {branch}"`

**Notes:**
- `LibGit2Sharp.Signature` requires name + email → from Git config or default
- Push requires credentials → SSH key or token
- Error on non-existent path → `ProviderException`

---

## GitHubSourceProvider
```
File: src/AgentSmith.Infrastructure/Providers/Source/GitHubSourceProvider.cs
```

**NuGet:** `Octokit` + `LibGit2Sharp`

Combined: LibGit2Sharp for Git ops, Octokit for PR creation.

**Constructor:**
- `string owner`, `string repo` (extracted from URL)
- `string token`
- `string cloneUrl`

**CheckoutAsync:**
1. Clone if not present: `LibGit2Sharp.Repository.Clone(cloneUrl, localPath)`
2. Create branch + checkout (same as LocalSourceProvider)
3. Return Domain `Repository(localPath, branch, cloneUrl)`

**CommitAndPushAsync:**
1. Stage + commit (same as LocalSourceProvider)
2. Push with token credentials: `UsernamePasswordCredentials`

**CreatePullRequestAsync:**
1. Create `GitHubClient` with token
2. `client.PullRequest.Create(owner, repo, new NewPullRequest(title, branch, "main"))`
3. Return: PR URL (`pullRequest.HtmlUrl`)

**Notes:**
- Clone target: Temp directory under `/tmp/agentsmith/{owner}/{repo}`
- PR is always created against `main` (configurable in the future)
- Extract owner/repo from URL (same logic as GitHubTicketProvider)

---

## Handler Updates

**CheckoutSourceHandler:** Stub → real implementation
```csharp
var provider = factory.Create(context.Config);
var repo = await provider.CheckoutAsync(context.Branch, cancellationToken);
context.Pipeline.Set(ContextKeys.Repository, repo);
```

**CommitAndPRHandler:** Stub → real implementation
```csharp
var provider = factory.Create(context.SourceConfig);
var message = $"fix: {context.Ticket.Title} (#{context.Ticket.Id})";
await provider.CommitAndPushAsync(context.Repository, message, cancellationToken);
var prUrl = await provider.CreatePullRequestAsync(
    context.Repository, context.Ticket.Title, context.Ticket.Description, cancellationToken);
context.Pipeline.Set(ContextKeys.PullRequestUrl, prUrl);
```

---

## Tests

**LocalSourceProviderTests:**
- `CheckoutAsync_ValidRepo_CreatesBranch` (real temp Git repo)
- `CommitAndPushAsync_WithChanges_Commits` (real temp Git repo)

**GitHubSourceProviderTests:**
- `CreatePullRequestAsync_ValidInput_ReturnsPrUrl` (mocked Octokit client)


---

# Phase 3 - Step 2: Ticket Providers

## Goal
Real implementations for fetching tickets from external systems.
Project: `AgentSmith.Infrastructure/Providers/Tickets/`

---

## AzureDevOpsTicketProvider
```
File: src/AgentSmith.Infrastructure/Providers/Tickets/AzureDevOpsTicketProvider.cs
```

**NuGet:** `Microsoft.TeamFoundationServer.Client`

**Constructor:**
- `string organizationUrl` (e.g. `https://dev.azure.com/myorg`)
- `string project`
- `string personalAccessToken`

**GetTicketAsync:**
1. Create `VssConnection` with PAT
2. Get `WorkItemTrackingHttpClient`
3. `GetWorkItemAsync(int.Parse(ticketId))`
4. Map `WorkItem` → Domain `Ticket`
   - Title: `workItem.Fields["System.Title"]`
   - Description: `workItem.Fields["System.Description"]`
   - AcceptanceCriteria: `workItem.Fields["Microsoft.VSTS.Common.AcceptanceCriteria"]`
   - Status: `workItem.Fields["System.State"]`
   - Source: `"AzureDevOps"`
5. Not found → `TicketNotFoundException`

**Notes:**
- `VssBasicCredential` for PAT authentication
- WorkItem Fields are dictionary-based - use defensive access with `TryGetValue`
- Organization URL from config: `https://dev.azure.com/{organization}`

---

## GitHubTicketProvider
```
File: src/AgentSmith.Infrastructure/Providers/Tickets/GitHubTicketProvider.cs
```

**NuGet:** `Octokit`

**Constructor:**
- `string owner` (extracted from URL)
- `string repo` (extracted from URL)
- `string token`

**GetTicketAsync:**
1. Create `GitHubClient` with `Credentials(token)`
2. `client.Issue.Get(owner, repo, int.Parse(ticketId))`
3. Map `Issue` → Domain `Ticket`
   - Title: `issue.Title`
   - Description: `issue.Body`
   - AcceptanceCriteria: `null` (GitHub Issues don't have this)
   - Status: `issue.State.StringValue`
   - Source: `"GitHub"`
4. Not found → `TicketNotFoundException`

**Notes:**
- Extract owner/repo from the source URL: `https://github.com/{owner}/{repo}`
- `ProductHeaderValue("AgentSmith")` for API calls
- Be mindful of rate limiting (GitHub API limit: 5000/hour with token)

---

## FetchTicketHandler Update

Replace the stub in `FetchTicketHandler` with the real implementation:

```csharp
public async Task<CommandResult> ExecuteAsync(
    FetchTicketContext context, CancellationToken cancellationToken = default)
{
    logger.LogInformation("Fetching ticket {TicketId}...", context.TicketId);

    var provider = factory.Create(context.Config);
    var ticket = await provider.GetTicketAsync(context.TicketId, cancellationToken);

    context.Pipeline.Set(ContextKeys.Ticket, ticket);
    return CommandResult.Ok($"Ticket '{ticket.Title}' fetched from {provider.ProviderType}");
}
```

---

## Tests

**AzureDevOpsTicketProviderTests:**
- `GetTicketAsync_ValidId_ReturnsTicket` (mocked HTTP client)
- `GetTicketAsync_NotFound_ThrowsTicketNotFoundException`

**GitHubTicketProviderTests:**
- `GetTicketAsync_ValidIssue_ReturnsTicket` (mocked Octokit client)
- `GetTicketAsync_NotFound_ThrowsTicketNotFoundException`
