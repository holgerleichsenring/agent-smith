---
name: feedback_branch_check_at_start
description: "Don't trust env-header for current branch when starting work — run git branch --show-current first"
metadata:
  type: feedback
status: proposed
---
When a session starts and the env-header shows a branch (e.g. "Current branch: p0103-skills-external-repo"), that snapshot can be stale. The actual checked-out branch may be different.

**Why:** In April 2026 I committed p0104 + p0105 onto `p0103-cli-and-docs` because the env said `p0103-skills-external-repo`. The user noted this happens to them too — not a big deal, just naming, but stacks unrelated work into one PR.

**How to apply:** Before the first commit of new work in a session, run `git -C <repo> branch --show-current` to verify. If it's a feature branch from prior work, ask the user whether to start a fresh branch from main (or from the prior branch's base) or to stack on top.
