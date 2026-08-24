# Analyzer ci commands for a dbt repository (p0505)

**NOT YET MEASURED.** This file is the eval's output path, and the eval has not run
with credentials. It is committed empty of results rather than filled with a guess:
what the analyzer emits for a data repository is the question, and inventing an
answer here would defeat the phase.

`DataRepositoryAnalyzerEvalTests` is `[Trait("Category", "LiveLLM")]` and skips
loudly without a key. It was executed on 2026-08-23 in an environment with no
`AZURE_OPENAI_API_KEY` and no `OPENAI_API_KEY`, and reported:

```
SKIP: no AZURE_OPENAI_API_KEY / OPENAI_API_KEY in env — the eval tier is paid-API and opt-in.
```

To fill it in, from a checkout:

```
OPENAI_API_KEY=sk-... dotnet test AgentSmith.sln \
  --filter "FullyQualifiedName~DataRepositoryAnalyzerEvalTests"
```

The eval runs the project analyzer three times over a copy of
`Fixtures/DataFixture/dbt/clean` on an `InProcessSandbox`, overwrites this file with
the emitted `ci` block verbatim per run, and asserts only the entry count and that
the file exists. A split across the three runs is recorded as a split.

## Why it matters

Verify resolution consults declared ci commands FIRST, and those come from the
analyzer fresh every run. If the analyzer draws any build command for a dbt
repository, it wins and a profile's declared list is never reached — which would
make the successor phase's profile inert on exactly the repositories it targets.
p0504 flagged that premise as unproven and it is still unproven.
