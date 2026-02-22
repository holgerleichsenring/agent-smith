# Phase 20d: Implementation Notes — Agentic Detail Updates

## What was built

Real-time detail events from the agentic loop (step 7/9) that appear as Slack thread replies under the progress message. Users see what the AI agent is doing: reading files, writing code, running builds, compacting context.

## Why

Step 7/9 "Executing plan" can run for minutes. Before this phase, users stared at a static progress bar with no idea what was happening. Now they see a live thread with scout activity, file operations, build commands, and iteration progress. The main channel stays clean — all detail goes into the thread.

## Key decisions

- **`ReportDetailAsync` added to `IProgressReporter`** — a new method alongside the existing progress/error/done methods. In CLI mode it logs at Debug level (only visible with `--verbose`). In Redis/Slack mode it publishes a `Detail` bus message.
- **Detail events are fire-and-forget** — errors in `ReportDetailAsync` are swallowed. A failed Slack API call must never abort the agentic loop. This is enforced in `ToolExecutor.ReportDetail()` with a try/catch around `GetAwaiter().GetResult()`.
- **Thread replies use existing `_progressMessageTs`** — `SlackAdapter.SendDetailAsync` posts as a thread reply using the `thread_ts` of the progress message already tracked per channel. No new state needed.
- **`BusMessageType.Detail` added** — new enum value, same outbound stream. `MessageBusListener` routes it to `adapter.SendDetailAsync`.
- **All three AI providers instrumented** — ClaudeAgentProvider, OpenAiAgenticLoop, and GeminiAgenticLoop all call `progressReporter?.ReportDetailAsync` for iteration events. ToolExecutor reports file reads, writes, and command executions.
- **`IPlatformAdapter.SendDetailAsync` added** — interface method for thread-based detail posting.

## Additional fixes applied during Phase 20d deployment

Several issues were discovered and fixed while deploying and testing:

### Dispatcher fire-and-forget race condition
`HandleSlackEventsAsync` used `ctx.RequestServices` inside `Task.Run`, but the `HttpContext` is recycled after the request returns 200. Fix: capture `IServiceScopeFactory` (a singleton) before `Task.Run` and create a new scope inside the background task.

### ToolExecutor command timeout deadlock
`ReadToEndAsync` was started before `WaitForExitAsync`, causing a deadlock when `dotnet run` (a never-ending server process) was executed. Fix: wait for process exit first (with 60s timeout), then read stdout/stderr only after the process has terminated.

### TestHandler false positive on .csproj
`DetectTestCommand` returned `dotnet test` for any repo containing a `.csproj` file, even without test projects. Fix: `HasDotNetTestProjects` reads each `.csproj` and checks for `Microsoft.NET.Test.Sdk` package reference — the standard marker for .NET test projects.

### Execution prompt improvements
The AI agent's system prompt was missing guardrails. Added: explicit prohibition of long-running server processes (`dotnet run`, `npm start`), requirement to state intent before each tool call, and `dotnet test` alongside `dotnet build` as recommended verification commands.

### Dockerfile runtime → SDK
The runtime stage used `dotnet/runtime:8.0` which has no SDK tools. The agentic loop needs `dotnet build` and `dotnet test` inside the container. Changed to `dotnet/sdk:8.0`.

## Files modified

| File | Change |
|------|--------|
| `Contracts/Services/IProgressReporter.cs` | Added ReportDetailAsync method |
| `Application/Services/ConsoleProgressReporter.cs` | Detail → Debug log |
| `Infrastructure/Bus/BusMessage.cs` | Added BusMessageType.Detail + factory |
| `Infrastructure/Bus/RedisProgressReporter.cs` | Publishes detail messages |
| `Infrastructure/Providers/Agent/ClaudeAgentProvider.cs` | Passes IProgressReporter to ToolExecutor and AgenticLoop; improved execution prompt |
| `Infrastructure/Providers/Agent/ToolExecutor.cs` | ReportDetail on file ops and commands; fixed command timeout |
| `Infrastructure/Providers/Agent/AgenticLoop.cs` | Iteration detail events |
| `Infrastructure/Providers/Agent/OpenAiAgentProvider.cs` | IProgressReporter parameter |
| `Infrastructure/Providers/Agent/OpenAiAgenticLoop.cs` | Iteration detail events |
| `Infrastructure/Providers/Agent/GeminiAgentProvider.cs` | IProgressReporter parameter |
| `Infrastructure/Providers/Agent/GeminiAgenticLoop.cs` | Iteration detail events |
| `Infrastructure/Providers/Agent/ScoutAgent.cs` | Scout detail events |
| `Dispatcher/Adapters/IPlatformAdapter.cs` | Added SendDetailAsync |
| `Dispatcher/Adapters/SlackAdapter.cs` | Thread replies via _progressMessageTs |
| `Dispatcher/Services/MessageBusListener.cs` | Routes Detail messages to adapter |
| `Dispatcher/Extensions/WebApplicationExtensions.cs` | IServiceScopeFactory fix for fire-and-forget |
| `Application/Commands/Handlers/TestHandler.cs` | Smart test project detection |
| `Application/Commands/Handlers/AgenticExecuteHandler.cs` | Passes IProgressReporter to provider |
| `Dockerfile` | SDK instead of runtime for agentic loop |
| `config/agentsmith.yml` | agent-smith-test back to fix-bug pipeline |
