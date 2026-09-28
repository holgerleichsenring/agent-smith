# Experiential memory

Some things a run learns are not in the code and not in git: that a scanner finding on
this repo is a known false positive, that the integration tests need the emulator up,
that the team wants guard clauses over nested ifs. Without somewhere to put them, every
run works them out again. The memory store is that somewhere.

It lives in the target repository, under `.agentsmith/memory/`, as plain Markdown. Runs
read it and propose additions to it; people review those additions in the pull request
like any other change.

## What's in the store

```
.agentsmith/memory/
├── MEMORY.md                 # the index: one line per memory
├── emulator-before-tests.md
└── sql-injection-fp-report-builder.md
```

Each memory is one file, one fact:

```markdown
---
name: emulator-before-tests
description: Integration tests need the storage emulator started first
metadata:
  type: project
---

`dotnet test tests/Integration` fails with a connection refusal unless the
storage emulator is running. Start it in `prerequisites`, not inside a test.
```

`name` is the kebab-case slug and the file name. `description` is one line. `type` is
one of three:

| Type | For |
|---|---|
| `feedback` | How the people behind the repo want the work done. Written by a run, it carries `status: proposed` and only becomes policy once someone ratifies it. |
| `project` | Goals, constraints or state that the code and git history can't tell the next run. |
| `reference` | Pointers to things outside the repository. |

`MEMORY.md` holds one line per memory and never the content:

```markdown
# Memory index
- [emulator-before-tests](emulator-before-tests.md) (project) — Integration tests need the storage emulator started first
```

A missing store reads as empty, and a malformed entry is skipped with a warning; neither
fails a run.

## How a run uses it

The `code`, `security-scan`, `api-security-scan`, `legal-analysis` and `pr-review` pipelines
load `MEMORY.md` before the master starts and put it into the master's prompt as the
"Experiential memory index". Only the index goes in, so a big store stays cheap. The
bodies stay on disk until they are asked for.

Two tools work on the store:

- **`recall`** takes a query and returns the full bodies of matching memories. Plain
  text, a `[[slug]]` citation, and a `type:feedback` / `type:project` / `type:reference`
  facet all work. It is a pure read, so it is on every master surface, the read-only
  scan surface included.
- **`remember`** writes one memory (or updates one by name) and its index line. It
  writes nothing but `.agentsmith/memory/`, so it is available on scan surfaces too
  without letting a scan change code. A `feedback` entry is recorded as proposed.

What a run remembers is committed with the rest of its run record and shows up in the
pull request. Review it there: merge it and it holds for the next run, drop the file and
it's gone. A `feedback` entry keeps its `status: proposed` marker until someone ratifies it; no run takes the marker off.

In a design conversation `remember` has nowhere to write, because its sandboxes are read-only.

## What belongs in it

- One fact per file. Check the index first and update an entry instead of adding a
  second one on the same subject.
- Only what the code and git can't already tell. A convention the linter enforces or a
  decision already recorded in a phase spec doesn't need a memory.
- Delete a memory that turns out wrong. A stale memory is worse than none, because the
  next run acts on it.

Rules that should always apply to the code belong in `principles.md` (see
[The context file](context-file.md)), not in memory.
