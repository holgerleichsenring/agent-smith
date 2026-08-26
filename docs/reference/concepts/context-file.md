# The context file

Every stack agent-smith works in has a `.agentsmith/contexts/<name>/context.yaml`.
It is pasted verbatim into every master prompt, so the model is its reader — and a
field that is wrong is not merely dead weight, it is acted on.

## One test decides whether a field belongs

| Class | The test | Examples |
| --- | --- | --- |
| **Judgement** | Somebody DECIDED it, and no file in the repository states it. | `meta.purpose`, `quality.limits`, `behavior.*`, `integrations`, `data`, `state`, `decisions` |
| **Mechanism** | The orchestrator ACTS on it — a run behaves differently because of its value. | `meta.workdir`, `meta.domain`, `stack.lang`, `stack.image`, `stack.resources`, `prerequisites`, `methodology`, `registry_auth` |
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
