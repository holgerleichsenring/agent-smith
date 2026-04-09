# Decision Logging

Every architectural choice, tooling decision, and trade-off the agent makes gets logged. Not *what* it did, but *why*.

## decisions.md

During execution, the agent writes decisions to `.agentsmith/decisions.md` in the target repository:

```markdown
## Architecture

### DuckDB over direct OneLake access
RBAC setup via abfss:// too complex for first run. DuckDB gives us local
query capability without infrastructure dependencies. Revisit when RBAC
is configured.

### Repository pattern instead of direct EF Core calls
The codebase already uses this pattern in 3 other modules. Consistency
over convenience.

## Testing

### Integration test against real database
Mocking the repository would hide the actual SQL generation. The bug was
in the query, not the business logic.
```

## Why Not What

The code diff shows *what* changed. The commit message summarizes *what* was done. Decisions capture *why* — the reasoning that isn't visible in the code:

- Why this pattern over another
- Why a dependency was added or avoided
- Why a simpler approach was chosen over a more complete one
- Why a test was written one way and not another

## Decisions in result.md

Run results include a Decisions section grouped by category:

```yaml
---
ticket: "#57 — GET /todos returns 500 when database is empty"
result: success
---

## Decisions

### Architecture
- Used existing `ITodoRepository` instead of creating a new service — consistency with the rest of the module

### Error Handling
- Return empty array instead of 404 — RESTful convention for collection endpoints with no results
```

## Why This Matters

When the agent's code breaks six months later, you need to know what it was thinking. Was the decision a shortcut that should be revisited? Or a deliberate trade-off with good reasoning?

Decisions turn AI-generated code from a black box into something a team can maintain.

## Decisions in the Knowledge Base

When the [Project Knowledge Base](knowledge-base.md) is enabled, decisions from all runs are compiled into `.agentsmith/wiki/decisions.md` -- a structured, cross-referenced wiki article. The compiler identifies patterns (e.g., three independent runs choosing the same architectural pattern) and warns when a previous decision was reversed.
