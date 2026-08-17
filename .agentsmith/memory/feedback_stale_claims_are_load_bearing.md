---
name: feedback_stale_claims_are_load_bearing
description: "A stale declaration is not untidiness — it is a premise other decisions rest on, so correcting it un-decides them."
metadata:
  type: feedback
status: proposed
---
When a declaration, a doc comment or a generated diagram stops being true, the cost is
not the wrong sentence. It is the decision someone made afterwards **because** of it.
Correcting the claim un-decides that decision, so a correction is not finished until you
have asked what was built on top of it.

**Why:** Found 2026-08-17 (p0433). `CommandModelUse` declared `VerifyPhase` deterministic;
since p0420 it makes one model call per phase, and p0421 made that call the only thing
deciding whether a run delivered. Fixing the declaration was the easy half. The half that
mattered: "the PR step is deterministic" had been **load-bearing** for how failure-tolerant
that step needed to be — a step that only opens a PR can be retried cheaply, a step that
first asks a model can die on a rate limit after every unit of real work is done. That is
the p0350 shape, where a run lost two finished PRs to its own token bucket. The correction
created p0434; the wrong sentence alone would have created nothing.

Generalised from an audit that had found six or seven stale claims — `ships_code` in doc
comments, the control-flow diagram, `PromptOwnership.cs` — and had been treating them as
tidiness.

**How to apply:**
- When you correct a stale claim, ask: **what did someone decide because this was true?**
  Then check whether that decision still holds. Name what you find rather than fixing it
  in the same phase — a phase that makes a declaration honest must not also change
  behaviour, or it becomes the thing it exists to remove.
- Treat "this doc comment is out of date" as a defect report, not a cleanup task.
- A declaration nothing can contradict is a comment with a compiler. Where a claim is
  load-bearing, give the evidence a way to argue with it (p0433's reachability
  cross-check), rather than trusting the claim to stay true.
