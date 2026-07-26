---
name: feedback_phase_versioning
description: "Never mutate an IMPLEMENTED phase spec when scope changes — create a successor with letter suffix. Pre-implementation specs (still in planned/, never executed) may be edited in place."
metadata:
  type: feedback
status: proposed
---
The successor-not-mutation rule applies once a phase has been implemented (active/ or done/).
Pre-implementation specs that still live in planned/ and were never executed may be edited
in place — including foundational premise corrections caught before any code is written.

For implemented phases: when scope changes (new decisions, new steps, expanded goal),
DO NOT edit the original phase YAML. Create a successor phase whose number reflects the
CHRONOLOGY of when the new work was planned, with letter suffix for the relation.
Reference the predecessor via `requires:` and in the goal text.

**Why:** "Die Regel gilt für bereits implementierte" (2026-05-05). Earlier framing
("hab ich vorher nicht so gemacht") plus "ich sehe keinen grund, warum eine spätere
phase nun eine frühere nummer haben sollte" — applies once work is in flight or shipped.
The phase folder is an audit log ordered by when work was conceived; mutating a shipped
phase erases prior thinking and giving a late successor an early number hides chronology.

**How to apply:**
- If phase has only ever lived in planned/ → edit in place, no successor needed
- If phase reached active/ or done/ → original stays as committed, create successor
- Successor number = current maximum base number + letter suffix (so a new follow-on
  to p0089b created today, when p0103 already exists, becomes p0103c — not p0089c)
- Letter suffix is just the next free letter under that base, NOT a topical-grouping
  signal — predecessor relationship lives in `requires:` and goal text
- Successor's `requires:` includes the predecessor
- Successor's goal explicitly references what's being extended/superseded
- Applies to scope changes only, not typo fixes / formatting / comment edits
