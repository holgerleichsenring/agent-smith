# Expectations & durable dialogue

Two capabilities that belong together: the run negotiates *what counts as done* with you before it writes code, and it can wait for your answer for days without holding a single pod.

## The Soll block: negotiate the WHAT before planning

Every `fix-bug` run has a `NegotiateExpectation` step between analysis and planning (p0328). After the agent has actually reproduced and analyzed the problem — this is the point: the draft is grounded in the analysis, not in the raw ticket text — it writes a capped expectation block:

- **observed** — what actually happens today.
- **expected** — verifiable assertions about what must be true afterwards.
- **constraints** — what must not change.
- at most **one A-or-B question**, when the analysis surfaced a genuine fork.

That block (the "Soll" — German for "target state", the term stuck) is posted to the ticket and the dialogue channels. Implementation starts once you ratify it or edit it. Your edits are parsed back into the same schema and validated against the same caps as the model's draft — an operator can't smuggle a novel through the gate either.

The ratified expectation is the run's **acceptance contract**. It's rendered into the plan prompt, into the master's execution prompt, and into the PR body. When you review the PR you review it against the expectation you signed off on, not against your memory of what the ticket kind of meant.

Headless and timeout behavior is honest rather than convenient: an unanswered ratification times out to approve (default), but the run is stamped `unratified` — visible in the record, same as a headless run. A model that can't produce a valid draft after three validation-feedback retries fails the run loudly.

There's an eval harness behind this (p0329): anonymized historical tickets with human acceptance criteria are replayed against the drafting step, and two metrics fall out — expectation-hit-rate (drafts ratified verbatim) and first-PR-acceptance. The judge LLM only matches draft items to gold items; the scores are computed deterministically. If you run Agent Smith seriously, seed it with your own tickets.

## Durable dialogue: checkpoint at the ask, resume on the answer

Any question a run asks — the Soll ratification, a mid-run clarification, an approval — used to mean a process waiting on stdin or a chat timeout. Now it means a checkpoint (p0327):

1. The run serializes its pipeline context, releases its lease and its compute, and gets the status **`waiting_for_input`**. Pods gone, cost stopped. The question sits on the ticket / in the chat with its deadline.
2. A background sweeper (leader-elected, every 15 seconds) watches for answers and expired deadlines.
3. When your answer arrives — hours or days later — the run re-enters *at the asking step*, with the answer staged for exactly that consumer, under the same run id. It queues through the normal capacity queue like any other run; nothing jumps the line.

Approvals get a 3-day default deadline (configurable), and the default answer on expiry is **reject** — a run you never approved doesn't sneak into execution because you were on vacation.

The practical consequence: asking the human stops being expensive. A run that would rather ask than guess doesn't block a sandbox slot for two days, so the system can afford to ask whenever the ticket is genuinely ambiguous. That's the same philosophy as the [clarification gate](spec-dialogue.md#the-clarification-gate), applied mid-run.

## What you see

- Runs list: `waiting_for_input` runs are visibly parked, with the open question; queued resumes show like any queued run with their position.
- The ticket: the question as a comment, the ticket parked in `needs_clarification_status` where that applies.
- `result.md`: the ratified (or `unratified`) expectation, and any `ignored_instructions`.

## Next

- [Spec dialogue](spec-dialogue.md) — the conversational front door.
- [Lifecycle](lifecycle.md) — where these steps sit in the run.
- [Capacity & queueing](../reference/operations/capacity.md) — the queue a resumed run rides.
