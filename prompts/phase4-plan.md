# Phase 4 - Pipeline Execution

## Goal
Make the system work end-to-end: User enters `"fix #123 in todo-list"` →
Intent is recognized → Config loaded → Pipeline built → Commands executed sequentially.

---

## Components

| Step | File | Description |
|------|------|-------------|
| 1 | `phase4-intent-parser.md` | IntentParser: Regex-based, User Input → TicketId + ProjectName |
| 2 | `phase4-pipeline-executor.md` | PipelineExecutor: Command names → Build Contexts → Execute Handlers |
| 3 | `phase4-use-case.md` | ProcessTicketUseCase: Orchestrates the entire flow |
| 4 | `phase4-di-wiring.md` | DI Registration in Infrastructure + Host Program.cs |
| 5 | Tests | IntentParser, PipelineExecutor, UseCase (with mocks) |

---

## Dependencies

- **Phase 1-3** must be complete (they are)
- IntentParser is intentionally kept simple (Regex instead of LLM call)
  - Rationale: Simplicity, no API costs, deterministic
  - Can be extended later to Claude-based parsing
- PipelineExecutor uses the existing CommandExecutor
- ProcessTicketUseCase is the central entry point for Host/CLI

---

## Design Decisions

### IntentParser: Regex instead of LLM
According to architecture.md, a Claude call is planned. For Phase 4 we implement
a Regex-based variant that recognizes the most common patterns:
- `"fix #123 in todo-list"` → TicketId(123), ProjectName(todo-list)
- `"#34237 todo-list"` → TicketId(34237), ProjectName(todo-list)
- `"todo-list #123"` → TicketId(123), ProjectName(todo-list)

An LLM-based parser can be registered as an alternative later.

### PipelineExecutor: Context-Building
The central challenge: Build the appropriate ICommandContext from a command name
(string from YAML config). This requires a mapping:
- `"FetchTicketCommand"` → `FetchTicketContext` with data from Config + PipelineContext
- Each command needs different inputs
- Solution: A `CommandContextFactory` that handles the mapping

### ProcessTicketUseCase
Orchestrates the entire flow:
1. Load config
2. Parse intent
3. Find project config
4. Find pipeline config
5. Start PipelineExecutor
