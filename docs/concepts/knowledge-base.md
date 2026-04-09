# Project Knowledge Base

Agent Smith compiles accumulated knowledge from all runs into a readable, linked wiki under `.agentsmith/wiki/`.

## The Principle

Inspired by Andrej Karpathy's "LLM Knowledge Bases" pattern: instead of RAG and vector databases, use simple Markdown files in Git -- written and maintained by the LLM, readable by humans.

```
Karpathy:     raw/  --> LLM compiles --> wiki/  --> Q&A --> output
Agent Smith:  runs/ --> LLM compiles --> wiki/  --> Q&A --> Slack/CLI
```

Every statement in the wiki traces back to a specific run, decision, or security scan. No black-box retrieval -- everything is versioned in Git.

## Wiki Structure

```
.agentsmith/
  wiki/
    index.md           # Master index with backlinks
    architecture.md    # How the system is built and why
    patterns.md        # Detected code patterns and conventions
    decisions.md       # Compiled from all runs -- why, not what
    known-issues.md    # Recurring problems and their solutions
    security.md        # Security trend summary
    concepts/          # Domain-specific articles (auto-generated)
      PaymentService.md
      AuthFlow.md
    health/            # WikiLint reports
  runs/                # Source data (referenced by wiki articles)
  security/            # SARIF snapshots (referenced by wiki/security.md)
```

## Compiling the Wiki

### CLI

```bash
# Incremental -- only incorporate new runs since last compilation
agent-smith compile-wiki --project my-api

# Full rebuild from all runs
agent-smith compile-wiki --project my-api --full
```

### Automatic Compilation

The wiki can compile automatically after pipeline runs, subject to rate limiting:

```yaml
knowledge_base:
  compile_interval_minutes: 60    # max once per hour (default)
  compile_on_every_run: false     # explicit opt-in for frequent teams
  compile_model: haiku            # cost-effective model for compilation
```

Wiki commits go to the **default branch**, not the PR branch. The wiki is project-wide accumulated knowledge, not PR-specific output.

### What Gets Compiled

The LLM reads new run outputs (`plan.md`, `result.md`, decisions) and updates the wiki:

- **decisions.md** -- new decisions, cross-references to earlier runs that made the same choice
- **known-issues.md** -- recurring problems with root cause and solution
- **patterns.md** -- code patterns the agent consistently uses
- **concepts/** -- new articles when new domain concepts appear
- **index.md** -- updated with new articles and backlinks

## Querying the Wiki

Ask questions against the wiki from Slack or CLI. The `@agent-smith` prefix is required:

```
@agent-smith why do we use Repository Pattern in my-api?
@agent-smith what are the most common errors in PaymentService?
@agent-smith show me all auth decisions from the last 3 months
```

Queries run directly in the dispatcher (no container job). The dispatcher performs a sparse Git checkout of `.agentsmith/wiki/` only, which takes 2-5 seconds instead of a full clone.

Answers include source references:

```
Answer: Three independent runs (r03, r07, r12) chose Repository Pattern,
mainly for testability and consistency. Run r15 attempted direct EF Core
-- that led to test issues and was rolled back.

Source: wiki/decisions.md, runs/r15/result.md
Related: wiki/patterns.md#testing, wiki/known-issues.md
```

## Health Checks (WikiLint)

`wiki-lint` analyzes the wiki for inconsistencies, gaps, and suggests new articles:

```bash
agent-smith wiki-lint --project my-api
```

The health report identifies:

- **Inconsistencies** -- contradictions between wiki articles
- **Gaps** -- topics referenced in runs but missing from the wiki
- **New article candidates** -- topics with enough run data to warrant their own article
- **Suggested questions** -- questions the wiki should be able to answer but cannot

### Automatic Fix

```bash
agent-smith wiki-lint --project my-api --fix
```

With `--fix`, WikiLint creates a PR with cleanups for identified inconsistencies. Health reports are stored in `.agentsmith/wiki/health/`.
