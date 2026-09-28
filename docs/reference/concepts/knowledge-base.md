# Project knowledge base

Agent Smith can compile the run records it leaves in a repository into a readable wiki under `.agentsmith/wiki/`.

## The principle

Inspired by Andrej Karpathy's "LLM Knowledge Bases" pattern: instead of RAG and vector databases, use plain Markdown files in Git, written by the LLM and readable by humans.

```
runs/  -->  LLM compiles  -->  wiki/
```

Everything is versioned in Git next to the code it describes.

## Wiki structure

```
.agentsmith/
  runs/                # source data: one directory per run, with plan.md and result.md
  wiki/
    index.md           # table of contents linking every wiki page
    decisions.md       # architectural and design decisions across runs
    known-issues.md    # bugs, limitations and workarounds the runs discovered
    patterns.md        # coding patterns and conventions the runs established
    <concept>.md       # further articles when the run data warrants them
    .last-compiled     # the last run the wiki incorporated
```

## Compiling the wiki

Compilation is a CLI command you run against a project directory that contains `.agentsmith/runs/`:

```bash
# Incremental: only incorporate runs newer than the last compilation
agent-smith compile-wiki --project ./my-api

# Full rebuild from all runs
agent-smith compile-wiki --project ./my-api --full

# Dry run: show what would be compiled without executing
agent-smith compile-wiki --project ./my-api --dry-run
```

The command reads the `plan.md` and `result.md` of every new run, hands them to the model together with the existing `index.md`, and writes the wiki pages it gets back. It then records the newest run in `.last-compiled`, so the next incremental compile starts after it. Run records from runs that were aborted at the bootstrap gate are skipped.

The model follows the `knowledge-master` skill from the [skills catalog](../../how-it-works/skills-catalog.md). It synthesizes rather than copies, groups related decisions, and notes when a later run superseded an earlier decision.

`compile-wiki` writes the files into the project directory and stops there. Committing the wiki is up to you.
