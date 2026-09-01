# The context file

Every stack agent-smith works in has a `.agentsmith/contexts/<name>/context.yaml`.
It is pasted verbatim into every master prompt, so the model is its reader — and a
field that is wrong is not merely dead weight, it is acted on.

## One test decides whether a field belongs

| Class | The test | Examples |
| --- | --- | --- |
| **Judgement** | Somebody DECIDED it, and no file in the repository states it. | `meta.purpose`, `quality.limits`, `behavior.*`, `integrations`, `data`, `state`, `decisions` |
| **Mechanism** | The orchestrator ACTS on it — a run behaves differently because of its value. | `meta.workdir`, `stack.lang`, `stack.image`, `stack.resources`, `prerequisites`, `verify`, `probe`, `methodology`, `registry_auth` |
| **Reading** | A copy of something the repository still states for itself. | `stack.frameworks`, `stack.sdks`, `stack.testing`, `stack.ci`, `meta.version`, `arch.style`, `quality.principles` |

A reading is worthless on the day it is written and wrong soon after. `sdks` names a
package at a version the manifest is one install away from disagreeing with, and
nothing can tell which of the two is true. Deleting it removes no knowledge — it
removes a second copy that can only diverge.

A guessed LABEL is the same defect wearing a taxonomy. "We build in layers" would be
a judgement if anybody had decided it; what actually happens is that an agent skims
the tree once and picks a word from a list. That is why `arch` goes: not because
nothing reads it, but because a reader can act on it, and acting on a guess is worse
than acting on a silence.

`meta.purpose` is the counter-example that survives everything. What a module is FOR
appears in no file, and the ticket-to-repo classifier reasons from it before a
checkout exists — the most human field in the file and one of the few the
orchestrator reads.

`state` is the other one. Every entry there records a decision and what came of it,
so the file teaches a reader how the program got its shape. It is long because
entries have been wordy, which the 400-character index-line cap attacks directly —
not a reason to remove the one section written by whoever made the call.

## `verify` is what proves a change

The `verify` block names the commands that turn an edit into a verdict, in order.
A run executes exactly those, ahead of anything the analyzer infers and ahead of
the .NET entry-point discovery — a repository that states its gates is the
authority on them.

Take the commands from the pipeline the repository already has. In an
established estate the truth about "green" is already written down, and a second
copy invented here can only disagree with it.

```yaml
verify:
  - label: build
    command: dotnet build MySolution.sln
  - label: test
    command: dotnet test MySolution.sln
  - label: lint
    command: npm run lint
    when_present: package.json
```

A stage carries a label, one shell command, and optionally the path it needs
present. Where that path is absent the stage is skipped and says so, so one
declaration serves repositories of different shapes. A command that cannot fail
— `echo`, `true` — is refused at resolution rather than run: a declared gate
that cannot go red is not a gate.

A repository that declares nothing keeps working exactly as before.

## `verify_derived_from` says where the commands came from

In an established estate the truth about "green" is already written down. A real
reference estate keeps twenty-three CI files; the pipeline pins the Python version,
installs a JRE so PySpark runs, and executes the unit tests against a live cluster.
Nobody could have guessed any of that — and nobody had to, because the repository
states it.

So the block above is DERIVED, and `verify_derived_from` records what from:

```yaml
verify_derived_from:
  files:
    - azure-pipelines.yml
    - Makefile
  hash: sha256:0a1b2c…
```

The files are paths relative to this context's workdir, named by whoever derived the
stages. The hash is the framework's, stamped by the write path — a model never supplies
one, because a hash it invented would report drift on the very next run.

Every later run re-reads those few files and compares. That is a filesystem read in the
sandbox: no model call, no re-derivation. When they no longer match, the run says once
that the declaration may be stale, next to the verdict — and still runs exactly what is
declared. Whether to re-derive is the operator's call.

**What the hash cannot catch.** It sees the pipeline FILE move, not the TARGET move. A
cluster id, a schema name, a service connection can all change under a byte-identical
file. Nothing in a hash sees that; the stage does, when it runs and goes red.

A repository with no pipeline gets no stages and no `verify_derived_from`. An invented
gate can only disagree with the one the estate actually runs.
## `probe` is whether the target answers

`verify` proves the change; `probe` proves the environment the change depends on is
reachable and willing. It is one command, run through a shell at this context's
workdir after the prerequisites and before the coding agent starts.

```yaml
probe:
  target: the warehouse dev workspace
  command: sf org display --target-org devhub
```

A CLI that resolves authentication before it does anything else reds on a clean tree
without credentials. That is a fact about the measurement environment, not about the
command: with the credential it is the cheapest true statement about the estate a run
can buy, and buying it first turns a wrong or absent credential into infrastructure
instead of into a coding agent that cannot build.

Reference an injected credential by NAME — `$SF_USERNAME` — never by value; the shell
expands it inside the sandbox.

A refusal fails the run naming the target, the command and the exit code, and carries
**no captured output**. Output travels into the failure reason, the per-repository
result document and a comment on the ticket, and the masker only knows values the
framework holds — it never holds an injected credential, so a value it does not know
is one it cannot replace. The output tail goes to the run log alone.

The record tells three states apart. The target answered; the target refused; no probe
was declared. Only the first is silent. A sandbox backend that injects no credentials
— docker, in-process — does not ask at all and says so, because a refusal there would
be a fact about the backend rather than about the target.

## `state.done` is written by the run

When a phase finishes in a target repository, agent-smith writes the executed spec to
`.agentsmith/phases/done/{id}-{slug}.yaml` **and** the `state.done` line that names it,
in the context whose sandbox carried the change. Every repository that gets the record
file gets the line.

The line is an index entry, not an essay: the goal, cut at a word boundary, then
`-> .agentsmith/phases/done/…`. It is composed to FIT the cap rather than refused for
exceeding it — the step runs after the work is committed, so a refusal would fail a run
nobody could go back and shorten. The reasoning belongs in the spec the pointer names.

The entry is keyed by phase id and upserted: a re-run replaces its own line rather than
adding a second one under the same key, which would make the file unparseable. New
entries go first, so the section reads newest to oldest.

The edit is a splice. Everything else in the file — the schema header, your comments,
your flow style — is left exactly as it was.

## Where the classification lives

In the schema. Every field in `.agentsmith/context.schema.json` carries a `$comment`
beginning with `JUDGEMENT`, `MECHANISM` or `READING` and one clause saying why, and a
test asserts that no field is missing one. A field added without a classification
fails the build, so the next one is argued rather than assumed.

## Deprecated, not deleted

The schema root is `additionalProperties: false`. Removing a reading from
`properties` would not stop the file asking for it — it would start REFUSING it, and
every context written before would fail on its first run. So the readings stay
declared and stay accepted:

- an existing `context.yaml` keeps its readings on disk and still validates;
- a model that still offers one is accepted, not refused — the tool discards it;
- the tool description, the template and the shipped samples no longer ask.

Nothing is migrated and no installation is invalidated. A context loses its readings
the next time it is written, and not before.
