---
name: feedback_used_to_work_check_history
description: "When the operator says a flow 'used to work' and the current run hits a chain of unrelated-looking breakages, the right first move is to read git history of the involved files — not to troubleshoot each symptom in isolation"
metadata:
  type: feedback
status: proposed
---
When the operator says a previously-working flow is now broken and the troubleshooting cascade keeps revealing more breakage in unrelated layers, **stop** and run `git log -p` (or `git log -S 'failing-symbol'`) on the files that are failing. A returning human dev would think "wait, this used to work, what changed?" — at high speed I lack that muscle memory and end up patching symptoms one at a time.

**Why:** During the 2026-05-27 api-scan local-CLI session, we hit five independent walls in sequence (project lookup, repos: required, sandbox deleting source, bootstrap layout mismatch, half-implemented passive mode). Each was a real regression introduced by a recent feature, not a config problem. Reading the most recent commits on `BootstrapCheckHandler.cs` / `BootstrapGateHandler.cs` / config-resolver would have pointed at the multi-context / source-aware / repo-required changes in one pass, instead of three separate dead-end troubleshooting branches. Operator noted: feature creep + no human memory = symptoms-chase is the default failure mode.

**How to apply:** When the operator's words include "used to work", "war schon mal grün", "lief gestern", or when the troubleshooting cascade is past its second unrelated-looking failure — pause symptom-chasing and `git log --oneline -20 <failing-file>` first. Read the last few diffs that touched it. The regression is almost always one of those commits. Related: [[feedback_challenge_premises]].
