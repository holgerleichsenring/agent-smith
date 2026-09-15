# Coding Principles

<!-- agentsmith:principles composed=core+typescript status=proposed -->

Transferred by init-project from the authored universal core plus the
'typescript' language delta. RATIFY by reviewing this file in the init pull
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

# TypeScript Delta

<!-- agentsmith:principles-delta typescript v1 -->

## Additions

### Naming

- `camelCase` for variables, functions, methods, and properties;
  `PascalCase` for types, interfaces, classes, and enums; `kebab-case` for
  file names.
- No `I` prefix on interfaces (`TicketProvider`, not `ITicketProvider`);
  booleans read as predicates (`isValid`, `hasAccess`, `canRetry`).

### Layout and size

- A module (file) is one cohesive responsibility; a small type and the
  functions that operate on it may share a file. Keep modules under roughly
  200 lines and functions under roughly 30 — extract when exceeded.
- Public surface is exported deliberately; everything else stays
  module-private. No barrel files that re-export the world.

### Types and abstractions

- `strict` mode is on; `any` is banned (`unknown` + narrowing where a type
  is genuinely open). No non-null assertions (`!`) outside tests.
- Contracts are `interface`/`type` aliases; collaborators arrive as
  constructor or function parameters typed by those contracts — modules never
  reach out and instantiate their own replaceable dependencies.
- Prefer plain functions and object composition over class hierarchies;
  `readonly` properties and `as const` for immutable data.

### Error mechanics

- `async/await` throughout; no floating promises — every promise is awaited,
  returned, or explicitly voided with a reason.
- Throw `Error` subclasses (never strings); catch narrowly, log with the
  caught error object so the stack survives, and rethrow or translate at
  abstraction boundaries. An empty `catch` is forbidden.
- Expected failure outcomes are modeled in the return type (discriminated
  unions / result objects); exceptions are for the unexpected.

### Tests and tooling

- Tests are colocated with the source as `<module>.test.ts` (or the
  project's established `__tests__/` convention) and run by the project's
  standard runner (vitest/jest).
- Test names are sentences stating scenario and expectation
  (`describe`/`it`); arrange-act-assert inside.
- Prettier formats, ESLint (typescript-eslint, recommended-type-checked)
  lints; both run clean in CI.

## Overrides

- **One type per file** → relaxed to one responsibility per module: a type
  and its closely related functions share a file when they change together.
- **Max 120 lines per class** → replaced by the module/function limits above;
  much TypeScript code has no classes at all, and plain functions are the
  preferred unit.
- **Separate test project** → tests are colocated next to the source they
  cover; a separate test-only package is not the idiom.
- **Class-first composition with constructor-injected services** → module
  and function composition is equally idiomatic: passing collaborators as
  typed function parameters satisfies dependency inversion without classes.

---

## Project Specifics (ratified additions)

_None yet. Rules appended here are project-authored and survive re-init._
