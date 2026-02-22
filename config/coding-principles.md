# Agent Smith - Coding Principles

This file is loaded by the agent (Claude/OpenAI) at runtime and governs code quality.
Injected into the execution context via `LoadCodingPrinciplesCommand`.

---

## Language Rule (NON-NEGOTIABLE)

- **All text in code MUST be in English.** This includes:
  - Comments (inline, block, XML-doc)
  - Documentation (README, API docs, architecture docs)
  - Exception messages
  - Log messages
  - Variable names, class names, method names
  - Commit messages
  - PR titles and descriptions
  - Test names and descriptions
- No exceptions. The codebase is English-only.

---

## Hard Limits

- **Max 20 lines per method** - No exceptions. Extract helper methods.
- **Max 120 lines per class** - Split when needed. Separate responsibilities.
- **One type per file** - Class, interface, enum, record = its own file.

## Project Structure

Each project follows a consistent top-level folder convention:

- `Contracts/` — Interfaces (with contextual sub-directories like `Providers/`, `Slack/`)
- `Models/` — Records, DTOs, configuration classes, data objects
- `Entities/` — Domain entities (Domain project only)
- `Services/` — All functional code (handlers, factories, providers, configuration loaders, bus)
- `Extensions/` — Extension method classes (`ServiceCollectionExtensions`, etc.)
- `Exceptions/` — Custom exception classes

Rules:
- Factories, Handlers, and Configuration loaders live under `Services/` — never at root level.
- Cross-layer interfaces belong in `AgentSmith.Contracts`. Project-internal interfaces use a local `Contracts/` folder.
- No loose files at project root (except `Program.cs` in Host/Dispatcher).

## SOLID

- **S** - Single Responsibility: Every class has exactly one reason to change.
- **O** - Open/Closed: Extend via new classes, not by modifying existing ones.
- **L** - Liskov Substitution: Subtypes must honor the base class contract.
- **I** - Interface Segregation: Small, focused interfaces. No `IDoEverything`.
- **D** - Dependency Inversion: Depend on abstractions, never on implementations.

## Naming Conventions

- `PascalCase` for classes, methods, properties, events
- `camelCase` for parameters, local variables
- `_camelCase` for private fields
- Interfaces: `I` prefix (e.g. `ITicketProvider`)
- Async methods: `Async` suffix (e.g. `FetchTicketAsync`)
- Booleans: `Is`, `Has`, `Can` prefix (e.g. `IsValid`, `HasAccess`)

## Patterns

- **Tell, Don't Ask** - Command objects, don't query state and decide externally.
- **Immutable Value Objects** - Records with `init` properties where possible.
- **No Public Fields** - Always use properties.
- **Guard Clauses** - Validate early, return early.
- **Null Safety** - Nullable Reference Types enabled. Avoid `null` where possible.

## Command Pattern (MediatR-Style)

- Every command defines its own `ICommandContext` record with specific input data.
- Every handler implements `ICommandHandler<TContext>` for exactly one context type.
- The `CommandExecutor` resolves handlers via DI - no manual instantiation.
- Cross-cutting concerns (logging, error handling) live in the `CommandExecutor`.

## Dependency Injection

- No manual `new` for services, providers, or command handlers.
- Everything via constructor injection.
- Factories for provider resolution based on config.

## Error Handling

- Domain exceptions for business logic errors.
- `CommandResult` for expected errors in the pipeline.
- Exceptions only for unexpected errors.
- No empty `catch` blocks.
- Log before re-throwing.

## Testing

- Every public method has at least one test.
- Arrange-Act-Assert pattern.
- Mock only external dependencies (providers).
- Test class naming: `{Class}Tests`.
- Test method naming: `{Method}_{Scenario}_{ExpectedResult}`.

## C# Specifics

- Use .NET 8 features (primary constructors, collection expressions).
- `record` for value objects.
- `sealed` for classes not intended for inheritance.
- `readonly` where possible.
- File-scoped namespaces.
- Global usings in a central file.

## What NOT To Do

- No god classes (>120 lines = refactor).
- No magic strings (use constants or enums).
- No nested `if` blocks (>2 levels = extract).
- No commented-out code blocks.
- No `Console.WriteLine` (use logger).
- No hardcoded configuration values.
- No non-English text in code, comments, or documentation.
