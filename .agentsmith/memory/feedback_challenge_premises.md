---
name: feedback_challenge_premises
description: "At high working speed, faulty premises in specs go unchallenged. Pause to verify foundational assumptions BEFORE coding."
metadata:
  type: feedback
status: proposed
---
When the user grants velocity ("ohne zu fragen, durcharbeiten") and a spec is on the table, the failure mode is: take the spec premise at face value and execute. The user has explicitly flagged this as an unusual pattern of mistakes ("die Fehler die hier passieren in der Masse sind mir noch nie passiert. Das liegt einerseits an der Geschwindigkeit, andererseits daran, dass ich mich selten so entscheiden würde wie du. Mir scheint der kontext oder die fragestellungen sind nicht richtig.").

**Before implementing a non-trivial spec, especially one touching production-critical paths, verify:**

1. **Deployment reality.** Does the system the spec assumes (e.g. "the CLI image is in K8s") actually exist in the deployment? Check the K8s configs / IaC. If not, the spec is built on a phantom dependency.

2. **Existing-flow validity.** If the spec says "mirror what flow X already does", verify flow X is actually exercised in production. A code path that exists but has never run is not validation — it's lurking.

3. **Conceptual fit.** Does the spec's mental model match the user's? CLI as "dev tool" vs CLI as "spawnable runtime" is a fundamental disagreement that no amount of code can paper over. If unsure, ask one foundational question.

**Why:** Production consequences. The user's framing: "Fehlerhafte entscheidungen haben direkt konsequenzen." High-speed work doesn't excuse skipping the foundational check; it amplifies the cost of skipping it.

**How to apply:**
- For any phase that touches deployment, infrastructure, or cross-process boundaries: spend ~5 minutes checking the K8s/IaC configs in adjacent repos before writing code.
- When a spec assumes "X already works", grep for the actual execution evidence (logs in past run records, deployed env vars, etc.).
- One short clarifying question is cheaper than re-doing a full PR. Velocity ≠ silence on premises.
- If the spec itself was authored at high speed (e.g. by me in a prior session), treat it with the same suspicion as any other input — not as a contract.
