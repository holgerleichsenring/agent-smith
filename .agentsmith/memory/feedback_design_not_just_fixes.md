---
name: feedback_design_not_just_fixes
description: "When shipping fixes, ask 'does the system's behavior make sense?' — not just 'does the code compile and tests pass?\"
metadata:
  type: feedback
status: proposed
---
When fixing a bug, do not stop at "the technical symptom is gone". Question whether the system's BEHAVIOR around the bug makes sense in the first place. Examples that prompted this rule:

- p0179f fixed Approval not crashing on missing Plan. I never asked: should Approval still be there at all in the collapsed shape? (The decision said yes, but I should have re-asked instead of just honoring it.)
- p0179g fixed the catalog subpath so coding-agent-master loads. I never asked: why does this pipeline analyze ALL 5 repos for what is clearly a single-repo ticket? That's ~$0.40 of LLM cost burned BEFORE any agent has decided which repo matters.
- p0183 redesigned the dashboard. I never asked: why does the operator have to expand each step to see what's happening — what does the operator actually need to know at a glance?

**Why:** Operator works at a high-context level and cares about whether the system serves the goal, not just whether the code is technically correct. Shipping three back-to-back fixes that each pass tests but leave behavioral nonsense in place is worse than pausing to look at the bigger picture.

**How to apply:**
- After locating a bug, BEFORE writing the fix, ask: "is the surrounding behavior even sensible? would the operator want it this way?"
- Surface the design question to the operator alongside the fix proposal, even when the immediate fix is obvious.
- Do not silently fix-and-ship at speed. Pause, name the bigger question, let the operator direct.
- This rule extends [[challenge-premises]]: not just "is the spec right" but "is the system behavior right".
