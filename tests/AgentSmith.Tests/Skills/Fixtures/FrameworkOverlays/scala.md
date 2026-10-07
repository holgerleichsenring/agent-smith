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

## Limits

No limits — no Scala source states a size limit as a rule; see "Layout and
size".

## Artefacts

No artefacts — the project's formatter and linter configuration already
holds these rules where the stack keeps them, and a file restating them would
be a second place for them to disagree.
