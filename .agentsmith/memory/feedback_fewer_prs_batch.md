---
name: feedback_fewer_prs_batch
description: "Batch related fixes into ONE PR — don't spin a new branch+PR per small fix; per-PR context.yaml edits cause recurring merge conflicts."
metadata:
  type: feedback
status: proposed
---
Don't open a separate PR for every individual fix. Batch related fixes into a single branch/PR.

**Why:** (1) Each PR takes a long time to get through CI (the docker multi-arch builds alone are ~6–10 min), so many small PRs are slow for the operator to merge. (2) Every phase appends its entry just before `active: {}` in `.agentsmith/contexts/default/context.yaml`, so two parallel branches BOTH edit that same spot → a context.yaml merge conflict on whichever PR merges second, every single time (happened with p0225 #281 vs p0226 #282).

**How to apply:** Group a session's related fixes into one branch/one PR (multiple commits is fine — one per phase). Only open separate PRs when the work is genuinely independent AND won't both touch context.yaml. If work must be stacked, branch later work off the PREVIOUS branch (not `main`) so context.yaml stays linear and conflict-free. When a parallel-branch context.yaml conflict does happen, resolve by keeping ALL phase entries in order (p0224 → p0225 → p0226 → `active: {}`). Relates to [[feedback_no_silent_defer]] and [[feedback_one_decision_yaml_per_phase]].
