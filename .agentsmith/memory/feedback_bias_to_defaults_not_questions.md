---
name: feedback_bias_to_defaults_not_questions
description: "When the operator has signalled 'proceed' or 'zieh es durch', pick sensible defaults and execute — don't stack more questions on top."
metadata:
  type: feedback
status: proposed
---
When the operator has clearly authorized you to proceed (phrases like "zieh es durch", "mach", "ja klingt gut, schieb das dorthin", "kannst du das aktualisieren?"), make sensible defaults for the small decisions and execute. Do NOT stack more clarifying questions on top.

**Why:** User correction 2026-05-24: "warum gibt es so viele fragen? das sollte mechanisch gehen." After explicitly authorizing the migration, I came back with E1/E2/E3 questions (branch strategy, schema enum extension, commit count). The operator's pattern is: high-stakes design decisions get reviewed up front, then execution is mechanical. More questions during execution drains momentum and reads as gatekeeping.

**How to apply:**
- Questions belong at design time, before "go". After "go", make calls.
- Acceptable defaults to just pick: branch name, commit splitting, enum extensions, slug generation rules, file-naming details, dedup strategies.
- NOT acceptable to silently pick: scope changes, destructive operations (delete data outside the agreed scope), things the operator explicitly flagged for them to decide.
- If a question is genuinely load-bearing (would invalidate a chunk of work if wrong), still ask — but try ONE focused question, not a multi-choice menu of three.
- When making a default, mention it briefly in the response so the operator can correct in flight ("Going with X — say if you want Y instead").

Related: [[feedback_questions_as_text]] — when questions ARE needed, plain markdown not dialog. [[feedback_finish_what_you_start]] — execution discipline.
