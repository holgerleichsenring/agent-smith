# Phase 19a-3: DI Wiring + Program.cs Refactoring

## Goal

Replace the monolithic `Program.cs` with a clean, extensible structure using
extension methods and dedicated handler classes. `Program.cs` becomes ~30 lines.

---

## What & Why

**Problem:** `Program.cs` was ~500 lines — a god class violating every coding
principle. All HTTP endpoints, intent handlers, DI wiring, and helper functions
were mixed together. Adding Teams support would have made it unmanageable.

**Solution:** Split into focused, single-responsibility classes:

| Class | Responsibility |
|---|---|
| `DispatcherDefaults` | All magic strings/values in one place |
| `DispatcherBanner` | ASCII banner on startup |
| `ServiceCollectionExtensions` | DI wiring as fluent extension methods |
| `WebApplicationExtensions` | Endpoint mapping (`MapSlackEndpoints`, etc.) |
| `SlackMessageDispatcher` | Parse intent → route to correct handler |
| `SlackInteractionHandler` | Handle button callbacks (yes/no answers) |
| `SlackSignatureVerifier` | HMAC-SHA256 request verification, isolated + testable |
| `FixTicketIntentHandler` | Handle fix intents, spawn job, register state |
| `ListTicketsIntentHandler` | Query ticket provider, format and send list |
| `CreateTicketIntentHandler` | Create ticket, send confirmation |

**Convention over Configuration:** All defaults live in `DispatcherDefaults`.
No magic strings scattered across the codebase.

---

## Structure

```
AgentSmith.Dispatcher/
├── Program.cs                          ← ~30 lines
├── DispatcherBanner.cs
├── DispatcherDefaults.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs  ← AddRedis, AddCore, AddSlack, AddJobSpawner, AddIntentHandlers
│   └── WebApplicationExtensions.cs     ← MapHealthEndpoints, MapSlackEndpoints
├── Handlers/
│   ├── FixTicketIntentHandler.cs
│   ├── ListTicketsIntentHandler.cs
│   └── CreateTicketIntentHandler.cs
└── Adapters/
    ├── SlackMessageDispatcher.cs
    ├── SlackInteractionHandler.cs
    └── SlackSignatureVerifier.cs
```

---

## Key Design Decisions

**`AddJobSpawnerAsync` is async** — it performs a connectivity check (Docker ping
or K8s API call) at startup to give immediate feedback in logs. If the spawner
is unavailable, `IJobSpawner` is simply not registered. The `FixTicketIntentHandler`
handles the `null` case gracefully.

**Intent handlers are `Scoped`** — each Slack message creates a new scope,
so handlers get fresh dependencies per request.

**`SlackSignatureVerifier` takes `signingSecret` in constructor** — stateless,
no DI needed, fully testable without mocks.

**Teams-ready:** Adding Teams support means:
- `MapTeamsEndpoints()` in `WebApplicationExtensions`
- `TeamsMessageDispatcher` + `TeamsInteractionHandler` in `Adapters/`
- `Program.cs` unchanged

---

## Adding a New Platform (Teams example)

```csharp
// Program.cs — one line added:
app.MapHealthEndpoints()
   .MapSlackEndpoints()
   .MapTeamsEndpoints();  // ← add this
```

Everything else is self-contained in the Teams adapter classes.

---

## Files

| File | Change |
|------|--------|
| `Program.cs` | REWRITTEN — ~30 lines, only wiring + app.Run() |
| `DispatcherDefaults.cs` | NEW — all constants |
| `DispatcherBanner.cs` | NEW — extracted from Program.cs |
| `Extensions/ServiceCollectionExtensions.cs` | NEW — AddRedis, AddCore, AddSlack, AddJobSpawner, AddIntentHandlers |
| `Extensions/WebApplicationExtensions.cs` | NEW — MapHealthEndpoints, MapSlackEndpoints |
| `Handlers/FixTicketIntentHandler.cs` | NEW — extracted from Program.cs |
| `Handlers/ListTicketsIntentHandler.cs` | NEW — extracted from Program.cs |
| `Handlers/CreateTicketIntentHandler.cs` | NEW — extracted from Program.cs |
| `Adapters/SlackMessageDispatcher.cs` | NEW — intent routing |
| `Adapters/SlackInteractionHandler.cs` | NEW — button callback handling |
| `Adapters/SlackSignatureVerifier.cs` | NEW — HMAC verification |

---

## Success Criteria

- [ ] `Program.cs` is ≤ 30 lines
- [ ] No class exceeds 120 lines
- [ ] No method exceeds 20 lines
- [ ] No magic strings in any class (all in `DispatcherDefaults`)
- [ ] No `Console.WriteLine` — only `ILogger`
- [ ] `dotnet build` succeeds
- [ ] Slack fix/list/create still works after refactoring
- [ ] Adding a second platform requires zero changes to `Program.cs`
