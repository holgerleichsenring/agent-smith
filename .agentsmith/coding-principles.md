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

## Core Design Philosophy

Every class must have **one clearly named responsibility**. The class name alone
must tell you what it does — no ambiguity. If you cannot describe the class's
purpose in one sentence without using "and", it has too many responsibilities.

**Model responsibilities, don't move code.** When splitting a large class, don't
just extract methods into helper files. Ask: "What are the distinct responsibilities
here?" Each responsibility becomes its own type with its own interface, registered
in DI, and injectable wherever needed.

**Composition over inheritance.** Orchestrate behavior by injecting small,
focused services — not by building fat base classes. Base classes must be thin
(max 30 lines) and contain only template method scaffolding, never business logic.

**Small classes are non-negotiable.** Most service classes should be 20-60 lines.
A class reaching 80 lines is a warning. A class reaching 120 lines must be split
immediately. If a class is large, it has too many responsibilities — period.

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
  Not "one area of concern" — literally one reason. A class that parses JSON
  AND validates business rules AND writes to a log has three reasons to change.
- **O** - Open/Closed: Extend via new classes, not by modifying existing ones.
- **L** - Liskov Substitution: Subtypes must honor the base class contract.
- **I** - Interface Segregation: Small, focused interfaces. No `IDoEverything`.
  One method per interface is perfectly fine if that is the responsibility.
- **D** - Dependency Inversion: Depend on abstractions, never on implementations.
  Every service that is injected into another class must have an interface.

## Naming Conventions

- `PascalCase` for classes, methods, properties, events
- `camelCase` for parameters, local variables
- `_camelCase` for private fields
- Interfaces: `I` prefix (e.g. `ITicketProvider`)
- Async methods: `Async` suffix (e.g. `FetchTicketAsync`)
- Booleans: `Is`, `Has`, `Can` prefix (e.g. `IsValid`, `HasAccess`)
- **Class names must describe their single responsibility precisely.**
  `GateOutputHandler` not `HandleGateOutput`. `NpmAuditParser` not `AuditHelper`.
  `SwaggerSpecCompressor` not `ApiUtils`. The name IS the documentation.

## Patterns

- **Tell, Don't Ask** - Command objects, don't query state and decide externally.
- **Immutable Value Objects** - Records with `init` properties where possible.
- **No Public Fields** - Always use properties.
- **Guard Clauses** - Validate early, return early.
- **Null Safety** - Nullable Reference Types enabled. Avoid `null` where possible.
- **No Magic Values** - Use constants or configuration. Avoid hard-coded values.
- **Convention over Configuration** - make things configurable but keep it simple by sensible conventions.

## Class Design

### Services (20-60 lines typical)
- One public method (the operation), private helpers if needed.
- All dependencies via constructor injection.
- No internal state mutation — services are stateless unless explicitly
  managing a resource (e.g. connection pool).

### Factories
- Create and return objects. No business logic.
- One factory method per product type.
- Factory methods log their invocation for traceability.

### Parsers
- Take raw input (string, JSON, YAML), return typed output.
- One parser per format/ecosystem/protocol.
- Pure transformation — no side effects, no logging of business events.

### Builders
- Instance-based (not static). Registered as Transient in DI.
- Fluent API where appropriate (`.SetX().AddY().Build()`).
- Build methods return the product, never void.

### Base Classes
- **Max 30 lines.** Template method scaffolding only.
- Define the execution skeleton. Subclasses provide the specifics.
- Never contain business logic, parsing, or I/O.
- If a base class grows beyond 30 lines, extract the logic into
  injectable services that the base class orchestrates.

### Handlers / Consumers
- One handler per command/event/message type.
- The handler's `ExecuteAsync`/`Handle`/`Consume` method orchestrates
  by calling injected services — it does not contain the logic itself.
- 20-50 lines typical. If longer, extract a service.

## Static vs. Instance

- **Statics only for `Map()` or extension methods.** No static service classes.
- Implementation classes (builders, formatters, validators, parsers) are
  instance-based and registered in DI as `Transient`.
- Pure mapping functions (`Map()`, `Parse()`, `Convert()`) are acceptable
  as static private helpers within a class, but never as public API.
- Static requires a compelling reason and reviewer approval.

## Command Pattern (MediatR-Style)

- Every command defines its own `ICommandContext` record with specific input data.
- Every handler implements `ICommandHandler<TContext>` for exactly one context type.
- The `CommandExecutor` resolves handlers via DI - no manual instantiation.
- Cross-cutting concerns (logging, error handling) live in the `CommandExecutor`.

## Dependency Injection

- No manual `new` for services, providers, or command handlers.
- Everything via constructor injection (primary constructors).
- Factories for provider resolution based on config.
- Every injectable service has an interface in `Contracts/`.
- Registration is explicit — no assembly scanning magic.

## Error Handling

- Domain exceptions for business logic errors.
- `CommandResult` for expected errors in the pipeline.
- Exceptions only for unexpected errors.
- No empty `catch` blocks.
- Log before re-throwing.

## Implementation Workflow (NON-NEGOTIABLE)

Every feature phase MUST follow this order:

1. **Read phase prompt** — `.agentsmith/phases/active/p{NN}-*.md` contains requirements and scope.
2. **Plan before coding** — Explore codebase, design approach, get approval.
3. **Implement step by step** — Contracts first, then implementation, then DI, then tests.
4. **Build after each step** — `dotnet build`, fix errors immediately.
5. **Run ALL tests** — `dotnet test`, 0 failures before commit.
6. **Update `.agentsmith/context.yaml`** — Move phase from `planned`/`active` to `done`.
7. **Move phase file to `done/`** — Move from `active/` to `done/`.
8. **Commit** — One commit per phase, descriptive message.

Phase directory structure: `phases/planned/` → `phases/active/` → `phases/done/`.
The `.agentsmith/context.yaml` is the single source of truth for what has been built.

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

- No god classes (>120 lines = refactor immediately).
- No fat base classes (>30 lines = extract services).
- No static services (use instance + DI).
- No magic strings (use constants or enums).
- No nested `if` blocks (>2 levels = extract).
- No commented-out code blocks.
- No `Console.WriteLine` (use logger).
- No hardcoded configuration values.
- No non-English text in code, comments, or documentation.
- No "code moving" refactors — always model responsibilities.
- No classes named `*Helper`, `*Utils`, `*Manager` — name the responsibility.
