# Coding Principles

<!-- agentsmith:principles composed=core+scala+spark status=proposed -->

Transferred by init-project from the authored universal core plus the
'scala' language delta. It adds the framework overlays 'spark', which the component's manifest declares. RATIFY by reviewing this file in the init pull
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

# Scala Delta

<!-- agentsmith:principles-delta scala v1 -->

## Additions

### Naming

- `UpperCamelCase` for classes, traits, objects, and type aliases;
  `lowerCamelCase` for methods, values, and variables; packages are
  lowercase.
- Constants follow the project's established style — the Scala style guide
  writes them `UpperCamelCase`, Spark and Databricks write `ALL_CAPS`. Use
  whichever the code base already uses; never mix the two.
- Test names are sentences stating scenario and expectation, in the form the
  project's test framework uses.

### Layout and size

- A class and its companion object live in the same file; a `sealed` type and
  all of its subtypes live in the same file (the language requires it).
- Line width and formatting are what the project's formatter configuration
  says (`.scalafmt.conf`); do not restate a width here.
- No method, class, or file line limit is set: no Scala source states one as
  a rule (Spark's own lint ships its method- and file-length checks disabled;
  the Databricks "Rule of 30" is stated "in general"). Split by
  responsibility, as the core requires.

### Abstractions and composition

- Traits declare contracts. An interface that Java code implements and that
  carries default methods is an `abstract class` instead — Java cannot use a
  trait's default implementations.
- Public and implicit methods state their result type explicitly; an inferred
  type silently changes the API, and an untyped implicit can break
  incremental compilation.
- Always write `override` when overriding.
- Overriding `equals` also overrides `hashCode`, and `equals` takes `Any` —
  an `equals(other: Foo)` overloads instead of overriding.
- Case-class constructor parameters are never `var`: a mutated case class
  lands in the wrong hash bucket.
- No structural types (`{ def close(): Unit }`) — they dispatch by
  reflection.

### Error mechanics

- Catch `NonFatal(e)`, never `Throwable`, `Exception`, or a bare `case _` in a
  `catch` — those swallow fatal errors and control-flow throwables.
- No `return` inside a lambda or closure: it compiles to a thrown
  `NonLocalReturnControl` that a catch-all swallows, and Scala 3 deprecates
  it (use `scala.util.boundary` / `break`).
- No `???` and no `NotImplementedError` in committed code.
- Measure durations with `System.nanoTime`, never `currentTimeMillis` — the
  wall clock jumps.

### Tests and tooling

- Tests live in the build tool's test source set (`src/test/scala` under
  sbt, Maven, and Gradle) and run with the framework the project already
  uses (ScalaTest, MUnit, specs2).
- An expected failure asserts its specific type (`intercept[IllegalArgumentException]`),
  never `Exception` or `Throwable` — the test would pass on the wrong failure.
- The project's formatter and linter (scalafmt; Scalafix or scalastyle where
  configured) run clean.

### Allowed only with a visible exception

These are wrong by default and right only where the author says why, in the
project linter's suppression syntax (`// scalastyle:off println` …
`// scalastyle:on println`, `// scalafix:ok`) or, without a linter, a reason
comment on the line:

- `println` in production code — logging is the channel.
- Throwing an `Error` subtype — an `Error` means the JVM is broken; throw an
  `Exception`.
- `toUpperCase` / `toLowerCase` without `Locale.ROOT` — locale-dependent
  case mapping breaks identifiers (the Turkish-i problem).

## Overrides

- **One type per file** → SUSPENDED. A companion object and a sealed
  hierarchy must share their file; several small, closely related types may
  share one.
- **Fixed method/class line counts** → none apply; see "Layout and size".
- **Separate test project** → tests live in the build tool's test source set
  of the same project.

---

# Framework Overlay: spark

## All languages

- Never assign to a driver-side variable or mutate a driver-side object
  inside a function passed to a distributed operation (`map`, `foreach`,
  `filter`, a UDF): Spark defines that behaviour as undefined. Aggregate
  across executors with an accumulator instead.
- A value that must be exact is accumulated only inside an action — updates
  made inside a transformation may be applied more than once.
- A function passed to `reduce` (or `fold`, `aggregate`) is commutative and
  associative; Spark combines partial results in any order.
- A broadcast value is never modified after it is broadcast.
- `SparkContext` and `SparkSession` are created and used on the driver only —
  never inside a closure or UDF — and one `SparkContext` is active per JVM.
- A UDF whose result is not a pure function of its input is marked
  non-deterministic (`asNondeterministic`); Spark otherwise treats it as
  deterministic and may evaluate it once or several times.
- Every window aggregate states its frame (`rowsBetween` / `rangeBetween`) —
  Spark derives the default frame from whether the window is ordered. The
  functions whose frame Spark fixes take none: `row_number`, `rank`,
  `dense_rank`, `ntile`, `percent_rank`, `cume_dist`, `lag`, `lead`.

---

## Project Specifics (ratified additions)

_None yet. Rules appended here are project-authored and survive re-init._
