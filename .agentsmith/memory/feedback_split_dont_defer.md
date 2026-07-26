---
name: feedback_split_dont_defer
description: "Splitting phases is fine; deferring load-bearing wiring ('mach ich später') is NOT — it destroys the operator's testability"
metadata:
  type: feedback
status: proposed
---
2026-07-17, after shipping p0343–p0345: I deferred the load-bearing data wiring (real beat statuses, ledger panel, acceptance dispositions on the client, config actually served/seeded) as "named follow-ups" while shipping the surrounding UI shells. Operator verdict: "das 'mach ich später' zerreisst meine Testbarkeit. Woran soll ich denn sehen, was Sache ist?" — and: "wir hatten alles besprochen und es sollte nun fertig sein." The shipped UI looked exactly like what it was: scaffolding without its concept. This has happened too often.

**Why:** A surface whose load-bearing data is "later" cannot be tested or judged — the operator has nothing to verify against what was discussed. Green builds + many files ≠ the thing we agreed. Deferring the hard wiring while shipping the easy shell inverts the value order, and heuristic placeholders (keyword-guessed beats) make it worse by looking like data.

**How to apply:**
- Splitting a too-big phase is ALWAYS fine: split VISIBLY (own p-numbers), work the slices sequentially, and use parallel agents in isolated worktrees to protect context.
- But every slice ships COMPLETE: UI + its real data + any migration/seed it needs, end to end. A slice boundary is a vertical cut, never "UI now, data later".
- Never ship a component that renders placeholders or heuristics where its concept should be. If the data doesn't exist yet, build the data FIRST or don't ship the surface.
- "Deferred" is only legitimate as a visible, agreed phase split — never as a quiet TODO inside a shipped surface.

Related: [[feedback_no_silent_defer]], [[feedback_finish_what_you_start]], [[feedback_no_heuristic_for_missing_data]] (the keyword-guessed beats were exactly that heuristic).
