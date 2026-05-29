# p0177 — external validation against Anthropic's Agent tool

External comparison note. Anthropic's own Agent tool — as documented
publicly — implements the same primitive p0177 ships:

- One-shot briefing in, one-shot result out.
- Sub-agents run in parallel and isolated; no knowledge of each other.
- Orchestrator sees only the final message, not the intermediate tool
  calls.
- Token separation per sub-agent (own context window).
- Optional turn-based continuation by agent ID; no live exchange.

That is byte-for-byte our SubAgentSpec → SubAgentResult contract,
plus our SemaphoreSlim-bounded parallel execution. Direction
validated externally.

## Where p0177 deliberately goes beyond Anthropic's shape

Anthropic's documented workaround for sub-agent-to-master handoff
loss is **extensive prompt engineering with exact output formats**.
That's discipline, not structure. It works for pure code research
where the worst loss is a sub-agent describing a finding the master
then has to re-confirm.

For security review and coding with sandbox mutation, that's not
enough. The gap between "what the sub-agent put in its `ResultText`"
and "what the sub-agent actually did to the world" becomes real and
expensive: a coding sub-agent that wrote a file in the sandbox but
described it incompletely; a security sub-agent that found a CVE
but summarised it into low-severity prose.

p0177's answer:

1. **Typed events on the shared bus** (p0173e contract +
   L2SubAgentEvents) — the sub-agent emits structurally what it did
   (FileWritten, Finding, Observation, ToolCall), not a story
   about what it did.
2. **Lazy access via `read_sub_agent_observations`** — the master
   pulls the bus events it needs to reason about a child's work,
   instead of relying on a distilled summary returned through the
   spawn boundary.
3. **`SubAgentResult` carries decision anchors only** — counts,
   IDs, status, cost. Never `ResultText`. Reflection-enforced via
   `SubAgentResult_HasNoResultTextField_Reflection`.

That is structural where Anthropic's docs lean on prompt
discipline. It's not gold-plating — it's the right answer to a
class of cases Anthropic's standard product doesn't run.

## The shared-sandbox decision (made explicit here)

Anthropic's sub-agents share **nothing** — no sandbox, no repo, no
working set. They overlap on read; the orchestrator deduplicates
afterwards. That works because they read.

Our sub-agents **also write**. The shared sandbox is therefore not
a duplication of Anthropic's model but a deliberate answer to a
case Anthropic's model structurally doesn't have. The sandbox-write
race that surfaced in the first p0177 review is the price of the
write-capable shape; the deliberate decision is:

- Children share one sandbox so multi-file work cohabits without
  cross-sandbox propagation cost.
- The deterministic spec-order merge of `SubAgentResult` rows is
  the master's view of who-did-what; the bus events are the
  fine-grained record.
- Operators who care about cross-child write conflicts read the
  bus, not the merge.

This is the structural cost of `write_file` on the sub-agent
surface. It's why Anthropic's read-only shape ships fewer
ceremonies — and why ours can't.

## Sharpening landed: parallel ↔ sequential as a per-call lever

Anthropic's docs name **parallel as default, sequential as
deliberate** when knowledge needs to flow between sub-agents.
p0177 today is parallel-only. The right home for the choice is at
the `spawn_agents` tool call — per call, not as a global config —
because the master is who knows whether the task needs
independence (parallel; MAD perspectives) or chaining (sequential;
prepare-then-execute).

The follow-up spec for this is `p0177a-spawn-agents-mode` in
`.agentsmith/phases/planned/`. The cut adds a `mode` parameter
to `spawn_agents` and a sequential branch in `SubAgentRunner`
where each task sees the prior task's decision-anchor summary
(NOT distilled text — still single-source-of-truth = bus).

## Summary

- Architecture direction validated externally.
- Refinements (typed bus, lazy access, shared sandbox) are
  deliberate extensions exactly where our use case goes beyond
  pure research.
- Parallel/sequential as a per-call master decision is the right
  small extension; `p0177a` spec captures it.
