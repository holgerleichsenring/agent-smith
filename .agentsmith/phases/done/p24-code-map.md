# Phase 24: Code Map Generation (Deep Codebase Understanding)

## Goal

Generate a `code-map.context.yaml` that maps internal codebase structure:
interfaces, implementations, key classes, dependencies, DI graph. Gives the
agent deep understanding of WHERE things are and HOW they connect.

## Why Not Just Read the Files?

Without a map, the agent doesn't know WHICH files to read. On a 500-file
codebase, reading everything costs ~100k tokens. The code map costs ~800 tokens
and tells the agent exactly where to look.

## Approach: LLM-Assisted, Not Custom Parsers

Feed key project files to the LLM and let it extract structure. Cost per
generation: ~5,000 tokens.

Input per language:

**.NET:** `*.csproj` files, `*.cs` file listing, `I*.cs` interface files,
`Program.cs`/`Startup.cs` (DI registrations).

**Python:** `*.py` listing, `__init__.py`, files matching `*interface*`/
`*abstract*`/`*protocol*`, `main.py`/`app.py`, top 20 files by size.

**TypeScript:** `package.json`, `*.ts`/`*.tsx` listing, `index.ts` barrels,
`*service*`/`*controller*`/`*provider*` files, route definitions.

## Output Format

```yaml
# code-map.context.yaml
generated: 2026-02-22T18:00:00Z
lang: csharp

modules:
  AgentSmith.Domain:
    path: src/AgentSmith.Domain/
    deps: []
    key-types:
      - TicketInfo: "Ticket entity with title, body, labels"

  AgentSmith.Contracts:
    path: src/AgentSmith.Contracts/
    deps: [Domain]
    interfaces:
      ITicketProvider: [FetchAsync, UpdateStatusAsync, CloseAsync]

entry-points:
  - src/AgentSmith.Cli/Program.cs: "CLI + DI wiring"

dependency-graph: "Domain ← Contracts ← Application ← Host"
```

## When to Generate

- During bootstrap (Phase 22) — alongside `.context.yaml`
- On request ("regenerate code map")
- NOT automatically on every run

## Architecture

- `ICodeMapGenerator` interface in Contracts
- `CodeMapGenerator` implementation in Infrastructure
- Uses same `IAgentProvider` — one LLM call
- Code map committed alongside `.context.yaml`
- Agent reads code map at start of pipeline

## Definition of Done

- [ ] Code map generation for .NET projects
- [ ] Code map generation for Python projects
- [ ] Code map generation for TypeScript projects
- [ ] LLM prompt produces valid, useful output
- [ ] Code map loaded in pipeline (new step or part of AnalyzeCode)
- [ ] Integration test: generate for Agent Smith's own repo
- [ ] Integration test: generate for sample Python project
- [ ] Integration test: generate for sample TypeScript project
