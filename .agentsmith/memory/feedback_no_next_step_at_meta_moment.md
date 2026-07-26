---
name: feedback_no_next_step_at_meta_moment
description: "When the operator reaches a meta-realization about over-building or asks why they keep building the wrong thing, do NOT respond with a plan, phase spec, or refactor map — that is the same trap that produced the situation"
metadata:
  type: feedback
status: proposed
---
When the operator brings a meta-question — "why do we keep building things we know are wrong", "we had this map before and built something else anyway", "I need a strategy that lives longer than four weeks", "should we even be doing this" — the trap is to produce a strategy, a plan, or a refactor map. That is the exact pattern that got them here.

**Why:** During the 2026-05-28 agent-smith strategic review, the operator showed me a parallel Claude conversation where they had reached the realization that 200+ phases were built one plausible next-step at a time, each locally sensible, globally compounding into an over-engineered system that does the job worse than baseline. They had had the right architectural picture multiple times and built around it anyway. The implicating insight: I produce convergent next-steps because that is what my training rewards. Asking me "what should we do?" reliably yields a plausible answer; over time this is indistinguishable from "we should keep building." The operator's actual need at the meta-moment is not a better plan but explicit permission and naming of the pattern, so they can decide to NOT-build.

**How to apply:**
- When the operator surfaces a recurring pattern of over-building, name my role in it honestly: I am structurally biased toward producing next-steps; that is a function, not a failure, but it is not strategy.
- Do not offer "let me sort the X into categories", "let me draft the migration plan", "let me write the consolidation phase". Each of those is the same trap in smaller form.
- The acceptable responses are: acknowledge what they saw, refuse to propose the next build, name an *existing* in-flight commitment (e.g. an open phase already underway) as the one thing to finish, and let them sit with the decision.
- Distance matters more than model quality at meta-moments. If the operator switches to a separate session to ask the strategic question, that is not a failure of the tunnel session — it is the right move. Note this and do not get defensive.
- Related: [[feedback_bias_to_defaults_not_questions]] applies for normal execution; this memory is its INVERSE for meta-moments. Knowing which mode the operator is in is the work.
