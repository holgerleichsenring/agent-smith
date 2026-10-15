# Decision logging

Every architectural choice, tooling decision, and trade-off the agent makes gets logged. Not *what* it did, but *why*.

## The log_decision tool

The agent records a decision by calling its `log_decision` tool with a category and a one-line description of the choice and its reasoning. The category is one of `Architecture`, `Tooling`, `Implementation` or `TradeOff`. The tool is available in every phase of a run.

Each decision goes to two places:

- The run's event stream, as a `DecisionLogged` event. This is the record the dashboard reads, and you can filter a run's trail down to it.
- A YAML file in the target repository, `.agentsmith/decisions/<run-id>.yaml`, which travels with the code on the run's branch.

The repository file has one entry per decision:

```yaml
run: 2026-05-20T22-27-43-8a3f
decisions:
  - category: Architecture
    chose: "Used the existing ITodoRepository instead of a new service: the module already follows that pattern"
  - category: Implementation
    chose: "Return an empty array instead of 404: a collection endpoint with no results is not an error"
```

## Which repository gets the file

In a run over several repositories, the file copy goes to the repository the decision is about. `log_decision` takes an optional `repository` argument for that. With one repository in the run you leave it out. With more than one it is required: a call without it, or with a name that isn't one of the run's repositories, gets an error back that lists the run's repositories, and the decision is not logged anywhere until a call names one. A repository counts once even when it runs in two toolchain sandboxes, and a template or reference checkout is never a target. A repository the agent adds mid-run counts from that point on.

During the documentation and test passes after the code is written, the choice is limited to the repositories the run changed.

The run's own decision list, the one in `result.md` and on the dashboard, is not split: it holds every decision of the run whichever repository got the file.

If the repository copy cannot be written, the decision is still recorded on the run and the agent carries on. A decision log never ends a run.

## Why, not what

The code diff shows *what* changed. The commit message summarizes *what* was done. Decisions capture *why*, the reasoning that isn't visible in the code:

- Why this pattern over another
- Why a dependency was added or avoided
- Why a simpler approach was chosen over a more complete one
- Why a test was written one way and not another

## Decisions in result.md

The run's `result.md` includes a Decisions section grouped by category:

```markdown
## Decisions

### Architecture
- Used the existing ITodoRepository instead of a new service: the module already follows that pattern

### Implementation
- Return an empty array instead of 404: a collection endpoint with no results is not an error
```

## Why this matters

When the agent's code breaks six months later, you need to know what it was thinking. Was the decision a shortcut that should be revisited? Or a deliberate trade-off with good reasoning?

Decisions turn AI-generated code from a black box into something a team can maintain.

## Decisions in the knowledge base

The [knowledge base](knowledge-base.md) compiles the run records under `.agentsmith/runs/`, including the Decisions section of each `result.md`, into `.agentsmith/wiki/decisions.md`, and notes when a later run superseded an earlier decision.
