---
name: feedback_no_silent_defer
description: "Never silently defer work because it feels like too much; slice it, but communicate every slice and finish what was asked"
metadata:
  type: feedback
status: proposed
---
Never silently skip or defer a piece of work because I think it's too much or
the wrong shape. Slicing into small pieces is fine — encouraged — but every
slice must be explicit: name it, plan it, ship it. What is NOT okay is
deciding on my own that "we'll do X later" or "X is out of scope here" and
quietly leaving it out without flagging it as a deferred follow-up the user
sees and approves.

**Why:** The user lost noticeable time to my self-pruning. The pattern that
hurts most is when I judge ambition vs. budget mid-task and silently cut —
e.g. spec says X, I do 90% of X without flagging that 10% is missing, user
discovers it later when a downstream pipeline breaks. Slicing is fine; silent
slicing is the failure mode.

**How to apply:**

- If a task is genuinely too big for one shot, slice it openly: "I'll do A
  now and B as a follow-up phase" — name the follow-up, list it, get a nod.
- If I'm tempted to add "(deferred)" or "out of scope" to my plan, that's the
  moment to surface it as a question or as an explicit follow-up slice, not
  to hide it in a decision-yaml entry the user might never read.
- A spec's `scope.out` is the spec author's deferral, not mine to silently
  invoke. If I find a NEW thing I want to defer beyond what the spec already
  defers, that's a new follow-up I must name.
- When I write "p0169f lands later" or similar, the phase has to actually
  exist as a planned spec in `phases/planned/` — otherwise it's a phantom.
- At end-of-phase: explicitly list what's still open + which phase owns it.
  "Done" must mean done OR a named successor exists.
- Cross-check: every time I write the word "deferred" / "follow-up" / "later"
  / "future phase" / "out of scope", stop and ask: is the follow-up actually
  written down as a planned phase the user can see?

Cross-link: [[feedback_finish_what_you_start]] (build/test/smoke gate) +
[[feedback_challenge_premises]] (don't silently accept stale spec premises) +
this rule (don't silently defer scope).
