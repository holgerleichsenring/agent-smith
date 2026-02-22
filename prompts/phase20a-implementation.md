# Phase 20a: Implementation Notes — Intent Engine

## What was built

A three-stage intent engine replacing the old `RegexIntentParser` + `ChatIntentParser`:

1. **ChatIntentParser** (regex stage) — pattern-matches common inputs at zero AI cost
2. **HaikuIntentParser** — calls Claude Haiku for free-form text that regex can't parse
3. **ProjectResolver** — deterministic project lookup via ticket provider APIs
4. **IntentEngine** — orchestrates all three stages into a single `ParseAsync` call

## Why

The old parser was fragile: it only handled exact regex patterns and failed silently on anything else. Users typing natural language ("please fix the login bug, ticket 58") got no response. The new engine gracefully degrades from free (regex) to cheap (Haiku) to deterministic (project lookup), ensuring every input gets a meaningful response.

## Key decisions

- **IntentEngine lives in AgentSmith.Dispatcher** — it is chat-specific logic, not core application logic. The CLI still uses the old `RegexIntentParser` in `AgentSmith.Application`.
- **ChatIntent hierarchy as discriminated union** — `abstract record ChatIntent` with subtypes (`FixTicketIntent`, `ListTicketsIntent`, `CreateTicketIntent`, `HelpIntent`, `GreetingIntent`, `ErrorIntent`, `ClarificationNeeded`). The dispatcher switch-cases on these.
- **Project resolver queries ticket providers in parallel** — if the user says "fix #58" without specifying a project, all configured providers are queried simultaneously. Single match = auto-resolve, multiple = ask user, none = error.
- **SlackMessageDispatcher** routes intents to dedicated handlers (`FixTicketIntentHandler`, `ListTicketsIntentHandler`, `CreateTicketIntentHandler`, `HelpHandler`).

## Files created

| File | Purpose |
|------|---------|
| `Services/IntentEngine.cs` | Orchestrator: regex → haiku → project resolution |
| `Services/HaikuIntentParser.cs` | Claude Haiku wrapper for free-form text classification |
| `Services/IHaikuIntentParser.cs` | Interface for DI |
| `Services/ProjectResolver.cs` | Parallel ticket lookup across all configured providers |
| `Services/IProjectResolver.cs` | Interface for DI |
| `Services/ChatIntentParser.cs` | Refactored regex stage (was `RegexIntentParser`) |
| `Models/ChatIntent.cs` | Intent type hierarchy |
| `Models/ProjectResolverResult.cs` | Resolution result (found/not found/ambiguous) |

## Files modified

| File | Change |
|------|--------|
| `Adapters/SlackMessageDispatcher.cs` | Routes resolved intents to handlers |
| `Extensions/ServiceCollectionExtensions.cs` | DI registration for new services |
| `Handlers/ListTicketsIntentHandler.cs` | Handles list-tickets intent |
| `Handlers/CreateTicketIntentHandler.cs` | Handles create-ticket intent |
