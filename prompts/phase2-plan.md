# Phase 2: Free Commands (Stubs) - Implementation Plan

## Goal
Implement all Free Command Contexts + Handlers as stubs.
Handlers have real signatures, logging, and error handling, but TODO bodies for the provider calls.
Additionally: `CommandExecutor` as a real implementation (the heart of DI resolution).

After Phase 2: The pipeline is "wired up" - commands run through but don't do anything real yet.

---

## Prerequisite
- Phase 1 completed (solution compiles, contracts defined)

## Steps

### Step 1: CommandExecutor Implementation
See: `prompts/phase2-executor.md`

The central class that resolves `ICommandHandler<TContext>` via DI.
The only non-stub implementation in Phase 2.
Project: `AgentSmith.Application`

### Step 2: Command Contexts
See: `prompts/phase2-contexts.md`

Define all 9 Free Command Context records.
Project: `AgentSmith.Application/Commands/Contexts/`

### Step 3: Command Handlers (Stubs)
See: `prompts/phase2-handlers.md`

All 9 Free Command Handlers with:
- Real constructor (DI-capable, takes factories/providers)
- Logging (ILogger)
- Guard clauses
- TODO in body for real logic
- Writes dummy data to PipelineContext
Project: `AgentSmith.Application/Commands/Handlers/`

### Step 4: DI Registration
`ServiceCollectionExtensions` in Application for handler registration.

### Step 5: Tests
- CommandExecutor tests (with mock handlers)
- At least 1 handler stub test (logging, guard clauses)

### Step 6: Verify
```bash
dotnet build
dotnet test
```

---

## Dependencies

```
Step 1 (CommandExecutor)
    └── Step 2 (Contexts) ← can be done in parallel with 1
         └── Step 3 (Handlers) ← requires Contexts + Executor interface
              └── Step 4 (DI Registration)
                   └── Step 5 (Tests)
                        └── Step 6 (Verify)
```

Steps 1 and 2 can be done in parallel, the rest sequentially.

---

## NuGet Packages (Phase 2)

| Project | Package | Purpose |
|---------|---------|---------|
| AgentSmith.Application | Microsoft.Extensions.Logging.Abstractions | ILogger<T> |
| AgentSmith.Application | Microsoft.Extensions.DependencyInjection.Abstractions | IServiceCollection Extensions |

---

## Definition of Done (Phase 2)
- [ ] CommandExecutor resolves handlers via DI and calls ExecuteAsync
- [ ] All 9 Free Command Contexts defined
- [ ] All 9 Free Command Handlers implemented as stubs
- [ ] Each handler logs start/end and has guard clauses
- [ ] DI registration for all handlers present
- [ ] CommandExecutor tests green
- [ ] `dotnet build` + `dotnet test` error-free
- [ ] All files adhere to coding principles (20/120, English)
