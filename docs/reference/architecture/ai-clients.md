# AI Clients

Agent Smith talks to every LLM provider through one abstraction: Microsoft's `IChatClient` from the `Microsoft.Extensions.AI` package family. Per-provider plumbing sits behind a small set of `IChatClientBuilder` implementations that each emit a configured `IChatClient`. Tool-bearing tasks are wrapped with `FunctionInvokingChatClient`, so the tool loop is run by the framework, not by hand-rolled code.

## The factory and the builders

```
IChatClientFactory
  ├─ ClaudeChatClientBuilder          claude, anthropic      (Anthropic.SDK 5.10.0)
  ├─ OpenAiChatClientBuilder          openai, azure_openai   (Microsoft.Extensions.AI.OpenAI, Azure.AI.OpenAI)
  ├─ GeminiChatClientBuilder          gemini, google         (Google_GenerativeAI.Microsoft 3.6.6)
  ├─ OllamaChatClientBuilder          ollama                 (OllamaSharp 5.3.10)
  ├─ CopilotChatClientBuilder         copilot                (GitHub.Copilot.SDK 1.0.14)
  └─ ExternalWorkerChatClientBuilder  external_worker        (an agent CLI on the host)
```

`IChatClientFactory.Create(AgentConfig agent, TaskType task)` resolves the builder by `AgentConfig.Type`, applies the per-task `ModelAssignment` from `ConfigBasedModelRegistry`, and builds the chain. The provider types the dashboard offers come from the registered builders' `SupportedTypes`, so anything you can pick is something the runtime can construct.

Which tasks get a tool loop is stated once, in `ChatClientFactory.ToolBearingTasks`: Primary, Scout, Planning, Reasoning, ContextGeneration and CodeMapGeneration. Summarization is the only task that gets a plain client. The loop's `MaximumIterationsPerRequest` defaults to 25; callers that run longer loops (the coding master, sub-agents) pass their own limit.

`AgentConfig` is per-pipeline runtime data, not a DI singleton; it is passed to each `Create` call. The builders and the factory are DI singletons.

## The chain, innermost first

| Layer | What it does |
|---|---|
| Provider client | The SDK's own `IChatClient`. The OpenAI and Azure SDKs have their retry policy set to zero attempts, so retries happen in one place. |
| `RateLimitingChatClient` | Token buckets per (provider, model) on requests and estimated input tokens. |
| `TransientRetryChatClient` | Retries dropped connections, 408 and 429 within `retry.max_retries`, honouring `Retry-After` up to 120 s. Every wait is logged with its reason. |
| `EventPublishingChatClient` | One `LlmCall` started/finished pair per provider call, priced from the agent's pricing table. |
| `RecordingChatClient` | Only when tracing is on: writes each prompt and answer to the run's trace. |
| `SensitiveToolHistoryScrubChatClient` | Replaces earlier results of credential tools, so the provider sees a secret once. |
| `ContextLengthRefusalChatClient` | Turns a provider's context-length refusal into a message naming the role, the window and the setting. |
| `ContextPressureFinalizingChatClient` | Tool loops with a stated window: forces a final answer at 85% of it. |
| `CompactingChatClient` | Folds old history into a summary under token pressure (see [Context compaction](../concepts/context-compaction.md)). |
| `MasterLoopGovernorChatClient` | Coding master only: budget fence and ledger reminders. |
| `FunctionInvokingChatClient` | The tool loop. |

The last four apply to tool-bearing tasks only.

On Claude the builder also puts a cache breakpoint on the latest message (`ClaudeHistoryCacheHandler`), so the conversation history is cached along with the system prompt and tools. Setting `cache.is_enabled: false` sends no cache directive.

## The tool surface

Tools are methods on small tool hosts under `AgentSmith.Application/Services/Tools/`, turned into `AIFunction`s through `AIFunctionFactory.Create`. `AgenticToolSurface` composes them per caller:

| Surface | Used by | Tools |
|---|---|---|
| `ReadWriteWithHuman` | coding master, docs generation | filesystem (read, write, edit, search, `run_command`), `log_decision`, human, optional web, credentials, `write_context_yaml`, `recall` / `remember` |
| `Scout` | scout sweep, repository analyzer | read-only filesystem, optional `web_fetch`; every result size-bounded |
| `Review` | scans and reviews | read-only filesystem, `http_request`, `log_decision`, `web_fetch`, `recall` / `remember`. No write and no `run_command`. |
| `SpecDialog` | design conversations | read-only filesystem, human, optional web, memory. No write and no `run_command`. |

Filesystem calls run through the sandbox (see [Sandbox architecture](../concepts/sandbox-architecture.md)). Change a tool method on its host, and every provider sees the new schema without touching anything else.

## Adding a new provider

1. Add the SDK and its `Microsoft.Extensions.AI` adapter to `AgentSmith.Infrastructure.csproj`, in lockstep with the M.E.AI core pin.
2. Implement `IChatClientBuilder` under `Infrastructure/Services/Factories/ChatClientBuilders/`, returning the SDK's `IChatClient`. Declare the `AgentConfig.Type` strings it claims in `SupportedTypes`, one name per provider.
3. Register it as `services.AddSingleton<IChatClientBuilder, MyChatClientBuilder>()`. The factory and the dashboard's provider list pick it up by type name.
4. Add a default rate budget for the type in `LlmRateBudget`, or it inherits the permissive local-model default.

## Why not `Microsoft.Extensions.AI.Anthropic`

The Microsoft Anthropic adapter is preview-only and embeds a vendored SDK fork. `tghamm/Anthropic.SDK` supports prompt caching, extended thinking, vision and MCP, and from 5.10.0 it implements `IChatClient` natively on `AnthropicClient.Messages`.

## Why not `Microsoft.Extensions.AI.Ollama`

It is preview-only and was abandoned at `9.7.0-preview`. `OllamaSharp`'s `OllamaApiClient` implements `IChatClient` natively. It is pinned to 5.3.10 because the 5.4.x line requires a newer `Microsoft.Extensions.AI.Abstractions` than the pin below allows.

## The 10.3.0 pin

`Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.OpenAI` are pinned exactly to `[10.3.0]`, and `Microsoft.Extensions.AI.Abstractions` the same way in `AgentSmith.Contracts`. The reason is tghamm/Anthropic.SDK#197: `Anthropic.SDK 5.10.0` calls `HostedMcpServerTool.get_AuthorizationToken()`, whose signature changed in 10.4, and the resulting `MissingMethodException` breaks any call that processes MCP server tools. The pin moves once that issue is closed upstream.

## Why the Copilot SDK is pinned exactly

`GitHub.Copilot.SDK` is pinned to `[1.0.14]` because its API moved between releases. The project sets `CopilotSkipCliDownload=true`, so no build downloads the Copilot runtime; an image that runs a Copilot agent places it explicitly (see [AI providers](../../connect-your-stuff/ai-providers.md#github-copilot)).
