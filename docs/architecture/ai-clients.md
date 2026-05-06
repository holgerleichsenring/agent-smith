# AI Clients

Agent Smith talks to all five LLM providers (Claude, OpenAI, Azure OpenAI, Gemini, Ollama) through one abstraction: Microsoft's `IChatClient` from the `Microsoft.Extensions.AI` package family. Per-provider plumbing is hidden behind a small set of `IChatClientBuilder` implementations that each emit a configured `IChatClient`. Tool-bearing tasks are wrapped with `FunctionInvokingChatClient` so the tool loop is handled by the framework, not by hand-rolled code.

## The factory and the builders

```
IChatClientFactory
  ├─ ClaudeChatClientBuilder       (Anthropic.SDK 5.10.0 — AnthropicClient.Messages : IChatClient)
  ├─ OpenAiChatClientBuilder       (Microsoft.Extensions.AI.OpenAI — handles openai + azure-openai)
  ├─ GeminiChatClientBuilder       (Google_GenerativeAI.Microsoft 3.6.6)
  └─ OllamaChatClientBuilder       (OllamaSharp 5.4.24 — OllamaApiClient : IChatClient)
```

`IChatClientFactory.Create(AgentConfig agent, TaskType task)` resolves the right builder by `AgentConfig.Type`, applies the per-task `ModelAssignment` from `ConfigBasedModelRegistry`, and wraps tool-bearing tasks (Primary / Scout / Planning) with `FunctionInvokingChatClient` configured for `MaximumIterationsPerRequest = 25`. Tasks that don't take tools (ContextGeneration / CodeMapGeneration / Summary) get the bare `IChatClient`.

`AgentConfig` is per-pipeline runtime data, not a DI singleton — pass it to each `Create` call. The four `IChatClientBuilder`s and the factory itself are DI singletons.

## The tool surface

`SandboxToolHost` is the single source of truth for the LLM tool surface. Seven `[Description]`-annotated methods become `AIFunction`s via `AIFunctionFactory.Create`:

| Method        | What                                                  | Backing                             |
|---------------|-------------------------------------------------------|-------------------------------------|
| `ReadFile`    | Read repository-relative file                         | `Step{Kind=ReadFile}` via `ISandbox`  |
| `WriteFile`   | Write repository-relative file                        | `Step{Kind=WriteFile}` via `ISandbox` |
| `ListFiles`   | List directory entries                                | `Step{Kind=ListFiles}` via `ISandbox` |
| `Grep`        | Regex/glob search                                     | `Step{Kind=Grep}` via `ISandbox`      |
| `RunCommand`  | Shell command via `/bin/sh -c` with stdout/stderr     | `Step{Kind=Run}` via `ISandbox`       |
| `LogDecision` | Architectural / Tooling / Implementation / TradeOff   | `IDecisionLogger`                   |
| `AskHuman`    | Interactive question with optional multiple-choice    | `IDialogueTransport`                |

`SandboxToolHostExtensions.GetAllTools(host)` returns all 7 as `IList<AITool>`. `GetScoutTools(host)` returns the 3 read-only ones (ReadFile / ListFiles / Grep) for the codebase-discovery path.

## Adding a new provider

1. Add the SDK + `Microsoft.Extensions.AI.<Provider>` adapter to `AgentSmith.Infrastructure.csproj` (lockstep with the M.E.AI core pin).
2. Implement `IChatClientBuilder` under `Infrastructure/Services/Factories/ChatClientBuilders/` returning the SDK's `IChatClient` adapter. Declare the `AgentConfig.Type` strings the builder claims via `SupportedTypes`.
3. Register the builder as `services.AddSingleton<IChatClientBuilder, MyChatClientBuilder>()` in `AgentSmith.Infrastructure.ServiceCollectionExtensions`. The factory picks it up automatically by type name.
4. Add a derived `*CostTracker` under `Infrastructure/Services/Providers/Agent/Cost/` if the provider exposes cache-hit info via a non-standard `UsageDetails.AdditionalCounts` key.

## Why we did not use `Microsoft.Extensions.AI.Anthropic`

The Microsoft .Anthropic adapter is preview-only and embeds a vendored Anthropic SDK fork. `tghamm/Anthropic.SDK` is the production-grade Claude SDK in this ecosystem (1.8M+ downloads), supports prompt caching (FineGrained 4-set-points + AutomaticToolsAndSystem), extended thinking, vision, and MCP. From version 5.10.0 it natively implements `Microsoft.Extensions.AI.IChatClient` on `AnthropicClient.Messages`.

## Why we did not use `Microsoft.Extensions.AI.Ollama`

`Microsoft.Extensions.AI.Ollama` is preview-only and was abandoned at `9.7.0-preview` (no 10.x line). `OllamaSharp` is the de-facto IChatClient-for-Ollama story today: `OllamaApiClient` implements `IChatClient` natively. Pinned to 5.4.24 because 5.4.25 floats its `Microsoft.Extensions.AI.Abstractions` dep to 10.4.1 and breaks the lockstep pin (see #197 below).

## The 10.4.1 pin

`Microsoft.Extensions.AI` and its `.OpenAI` sibling are pinned EXACT to `[10.4.0]`. `Microsoft.Extensions.AI.Abstractions` is pinned the same way in `AgentSmith.Contracts`. Reason: tghamm/Anthropic.SDK#197 is open at the time of writing — `Anthropic.SDK 5.10.0` calls `HostedMcpServerTool.get_AuthorizationToken()` (existed at M.E.AI 10.3.0, signature changed in 10.4.1) and a `MissingMethodException` blows up any `IChatClient` call that processes MCP server tools. We don't actively use those tools, but the binding is fragile enough that the pin is the safer move. Bump in a follow-up phase once #197 is closed upstream.

## Cost tracking

`PipelineCostTracker.Track(ChatResponse)` reads the provider-agnostic `response.Usage.InputTokenCount` + `OutputTokenCount`, plus cache-hit info via `Usage.AdditionalCounts`. The polymorphic per-provider hierarchy from p0119 is preserved and adapted: `ClaudeCostTracker` reads `cache_read_input_tokens` / `cache_creation_input_tokens`; `OpenAiCostTracker` reads `cached_tokens`. Each migrated caller calls `PipelineCostTracker.GetOrCreate(pipeline).Track(chatResponse)` once after each response — the previous `TrackingLlmClient` decorator is gone.

## What this replaces

5 `*AgentProvider` implementations, 4 `*AgenticLoop` classes, 4 `*AgenticAnalyzer` adapters, 4 `*ToolDefinitions` files, the entire `ILlmClient` surface (interface, factory, 3 implementations, decorator, `LlmResponse` record), `ToolExecutor`, `IRepositoryToolDispatcher` + impl, `ScoutAgent`. About 2500 lines of provider-specific code, gone.

The bottom line: change a tool method on `SandboxToolHost`, all 5 providers see the new schema without touching anything else.
