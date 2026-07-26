---
name: feedback_no_heuristic_for_missing_data
description: "When a fix involves a temporal/structural heuristic around a missing data field, the data field is the fix"
metadata:
  type: feedback
status: proposed
---
When my proposed fix walks around a missing data field with a heuristic
(temporal proximity, name matching, regex extraction, "(see server logs)"
fallback), the heuristic IS the symptom that the data field is missing.
Add the field, don't paper over it.

**Why:** I shipped a p0169j-b spec with a 2-second-window ActivityErrorPairer
to fold step-fail + nearby explanation events into a composite ErrorRow.
The slice was motivated by a real diagnostic gap (skill-validator FAIL
caused a step crash, dashboard said "step failed" without why). But the
skill-validator FAIL isn't a pairable event — it's a server-log-only line.
The heuristic would have missed exactly the case it was built for and
rendered "(see server logs)". The user caught it: the fix belongs in the
backend — failing producers emit their reason as an event field. My
rationalisation ("don't re-open the p0169e contract") was the expensive
avoidance.

**How to apply:**

- If a UI spec adds a heuristic (temporal window, name-match, regex on
  free text) AROUND a class of events, ask: is the missing structured
  data the actual fix? Three times out of four it is.
- "Don't re-open the contract" is not a default. Re-opening a contract
  to add a typed reason field that prevents a heuristic is cheaper than
  shipping a heuristic that drifts under edge cases.
- If a backend producer fails or skips silently (FAIL log, swallowed
  exception, dropped from a collection), and a consumer downstream
  needs to know about it, that's an event-emission gap, not a
  consumer-side rendering problem.
- The completeness-test gate from p0169e ("silent producer = red")
  needs a failed-run counterpart: assert failure reasons populate the
  corresponding event fields. Otherwise the gap recurs in a new shape.
- When tempted to write "(see server logs)" as a fallback, stop. That's
  exactly the path the operator was supposed to escape.

Cross-link: [[feedback_no_silent_defer]] (don't quietly drop scope) +
[[feedback_challenge_premises]] (foundational premises in specs go
unchallenged) + this rule (don't paper over the missing field).
