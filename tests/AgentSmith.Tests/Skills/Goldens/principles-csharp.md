# Coding Principles

<!-- agentsmith:principles composed=core+csharp status=proposed -->

Transferred by init-project from the authored universal core plus the
'csharp' language delta. RATIFY by reviewing this file in the init pull
request and merging it. Project-specific rules go under "Project
Specifics" below; init-project re-runs preserve this file as-is.

---

# Universal Coding Principles (Core)

<!-- agentsmith:principles-core v1 -->

These principles are authored gold. They are authoritative: code moves toward
them — they are never inferred from the code that happens to exist. They state
INTENT only and apply to every language and framework. Everything that names a
mechanism (a keyword, a folder convention, a casing style, a library, a tool)
belongs in the language delta that is composed below this core, never here.

## Language

- All text in the codebase is English: names, comments, documentation,
  commit messages, error and log messages, tests. No exceptions.

## One responsibility per unit

- Every unit of code has exactly one clearly named responsibility. If its
  purpose cannot be described in one sentence without "and", it has too many.
- Name the responsibility. The name alone tells a reader what the unit does —
  the name IS the documentation. Names like Helper, Utils, or Manager hide the
  responsibility instead of naming it; name what the unit actually does.
- Model responsibilities, don't move code. When splitting a large unit, do not
  merely relocate lines: identify the distinct responsibilities and give each
  one its own unit with its own contract.
- Keep units small. A growing unit is accumulating responsibilities; split it
  by responsibility before it becomes load-bearing. Concrete size limits are
  set by the language delta and are enforced, not aspirational.

## SOLID

- **S**ingle responsibility: one reason to change per unit — literally one.
- **O**pen/closed: extend behavior by adding new units, not by editing
  existing ones into new shapes.
- **L**iskov substitution: anything that stands in for an abstraction honors
  that abstraction's full contract.
- **I**nterface segregation: contracts are narrow and focused. One capability
  per contract is fine when that is the responsibility; nothing depends on
  capabilities it does not use.
- **D**ependency inversion: depend on abstractions, never on concrete
  implementations. A unit receives its collaborators from the outside instead
  of constructing them itself.

## Simplicity

- **DRY**: one authoritative home per piece of knowledge. Duplicated logic
  drifts; extract it to a single owner.
- **YAGNI**: build what the current requirement needs, nothing speculative.
  Delete unused code instead of keeping it "just in case".
- **KISS**: prefer the simplest design that works. Cleverness that needs a
  comment to be understood is a defect, not a feature.
- Keep branching shallow: more than two levels of nested conditions means a
  smaller unit is hiding inside — extract it.
- Convention over configuration: make behavior configurable where it must be,
  and keep everything else on a sensible, predictable default.

## Composition over inheritance

- Build behavior by composing small, focused collaborators. Deep hierarchies
  couple everything to everything; composition keeps each piece replaceable.
- When a hierarchy is genuinely unavoidable, the shared parent stays a thin
  skeleton that delegates the real work to injected collaborators.

## Tell, don't ask

- Tell a unit to do its job; do not query its state and decide on its behalf.
  The logic lives with the data it operates on.

## Robustness

- Never silently swallow errors. Every suppressed failure leaves a trace that
  says what failed and why; a silent swallow makes failures undiagnosable.
- Distinguish expected outcomes from genuine failures, and signal each through
  the channel the language delta defines for it.
- Classify failures by their kind, never by parsing message text.
- Validate early and leave the failure path immediately (guard clauses); keep
  the happy path unindented and readable.
- No magic values: give every literal that carries meaning a name or a
  configuration home.
- Prefer immutable data. State that cannot change cannot be corrupted; allow
  mutation only where a unit explicitly manages a resource.
- No commented-out code and no dead code — version control remembers.

## Enforce with tests

- Every rule worth having is enforceable, and every enforceable limit has a
  test or an automated check. A principle nobody can check is a wish.
- Every public behavior has at least one test. Tests state the scenario and
  the expected outcome in their name, and follow arrange–act–assert.
- Replace only external collaborators with test doubles; test real behavior
  everywhere else.
- Work in small verified steps: build and run the tests after each change,
  and finish only when everything is green.

## Delta hooks

The language delta composed with this core MUST define the mechanisms for:

1. Naming style (casing, prefixes/suffixes, test naming).
2. Code layout (where units live, what shares a source unit, size limits).
3. Abstraction and composition idiom (how contracts are declared and how
   collaborators are supplied).
4. Error mechanics (how failures are signaled, propagated, and logged).
5. Test placement and tooling (where tests live, which framework runs them).
6. Formatting and lint enforcement (which tools make the rules checkable).

A delta may also OVERRIDE a habit that another stack would import when it
fights this language's idiom — the override names the rule it replaces and
states what applies instead.

---

# .NET / C# Delta

<!-- agentsmith:principles-delta csharp v1 -->

## Additions

### Hard limits (enforced)

- Max 20 lines per method — extract helper methods, no exceptions.
- Max 120 lines per class — split by responsibility when reached. Most
  service classes are 20–60 lines; 80 lines is a warning.
- One type per file: every class, interface, enum, or record gets its own
  file.
- Base classes max 30 lines: template-method skeleton only, never business
  logic, parsing, or I/O — inject services for anything complex.

### Naming

- `PascalCase` for classes, methods, properties, events; `camelCase` for
  parameters and locals; `_camelCase` for private fields.
- Interfaces carry the `I` prefix (`ITicketProvider`); async methods the
  `Async` suffix (`FetchTicketAsync`); booleans an `Is`/`Has`/`Can` prefix.
- Class names state the single responsibility: `NpmAuditParser`, not
  `AuditHelper`; `SwaggerSpecCompressor`, not `ApiUtils`.

### Project layout

- Consistent top-level folders per project: `Contracts/` (interfaces),
  `Models/` (records, DTOs, config types), `Services/` (all functional code:
  handlers, factories, providers, loaders), `Extensions/`, `Exceptions/`,
  `Entities/` (domain project only).
- Factories, handlers, and configuration loaders live under `Services/`,
  never at project root. No loose files at root except `Program.cs` in a
  host project.
- Cross-layer interfaces go to the shared contracts project; project-internal
  interfaces use a local `Contracts/` folder.

### Abstractions, DI, and composition

- Every injectable service has an interface in `Contracts/`; depend on the
  interface, never the implementation.
- All dependencies arrive via constructor injection (primary constructors).
  No manual `new` for services, providers, or handlers; factories resolve
  providers from config.
- Registration in the DI container is explicit — no assembly-scanning magic.
  Implementation classes (builders, formatters, validators, parsers) are
  instance-based and registered `Transient`.
- Statics only for `Map()`-style pure helpers and extension methods — never
  static service classes. Public static API needs a compelling reason.
- Command pattern (MediatR-style): every command defines its own context
  record; every handler implements the handler contract for exactly one
  context type; the executor resolves handlers via DI; cross-cutting concerns
  (logging, error policy) live in the executor.
- Config injection: inject `*Config`/`*Options` classes directly by concrete
  type registered as singleton. Do not wrap in `IOptions<T>` unless the value
  is genuinely reloaded from `IConfiguration` at runtime.

### Class design

- Services: one public method (the operation) with private helpers; stateless
  unless explicitly managing a resource; 20–60 lines typical.
- Factories create and return objects — no business logic; one factory method
  per product type.
- Parsers take raw input (string, JSON, YAML) and return typed output — pure
  transformation, no side effects.
- Builders are instance-based (never static), fluent where appropriate
  (`.SetX().AddY().Build()`); Build methods return the product, never void.
- Handlers orchestrate by calling injected services (20–50 lines typical) —
  they do not contain the logic themselves.
- No `Console.WriteLine` — route all output through the injected logger.

### Error handling

- Domain exceptions for business-rule failures; result objects for expected
  pipeline outcomes; exceptions only for the unexpected.
- Never an empty `catch` block — not even comment-only. Every catch body logs
  at least once (debug/trace floor for deliberately-swallowed expected
  exceptions, warning when unexpected); explanatory comments go ABOVE a log
  call, not instead of it.
- Catch the narrowest exception type that fits; classify on the exception
  type (`is OperationCanceledException`), never on `Exception.Message` text.
- Log with the exception object (stack trace survives) before re-throwing.

### Language idiom

- Target modern .NET: primary constructors, collection expressions,
  file-scoped namespaces, global usings in one central file.
- `record` for immutable value objects with `init` properties; `sealed`
  unless designed for inheritance; `readonly` where possible.
- Nullable Reference Types enabled; no public fields — properties only.

### Testing

- Tests live in a separate test project; test classes are named
  `{Class}Tests`, test methods `{Method}_{Scenario}_{ExpectedResult}`.
- Arrange-Act-Assert structure; mock only external dependencies (providers).

## Overrides

No overrides — this is the reference stack the mechanism vocabulary comes
from; the core's defaults map 1:1.

---

## Project Specifics (ratified additions)

_None yet. Rules appended here are project-authored and survive re-init._
