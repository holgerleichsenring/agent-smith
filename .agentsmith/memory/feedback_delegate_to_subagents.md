---
name: feedback_delegate_to_subagents
description: "Delegate independent/bounded work to background subagents instead of long in-context sequential work that blocks the user"
metadata:
  type: feedback
status: proposed
---
The user was held up while I implemented a large task (p0353 backend) in-context, sequentially, turn by turn. They pointed out: "warum spawnst du nicht einfach einen Subagent der den job dann macht? das hält mich doch auf."

**Why:** sequential in-context building makes the user wait on every build/test cycle; a background subagent frees them to do other things while the work runs.

**How to apply:** when remaining work is separable and bounded, spawn `Agent(run_in_background: true)` with FULL context baked in (exact file:line, all decisions pre-made, verify steps) and relay the result — don't make the user watch me edit. Keep only tightly-interdependent work that needs fast build/test loops on shared files in-context. Watch for build contention: two agents running `dotnet build`/`dotnet test` on the same projects in one working tree corrupt each other's obj/bin — sequence them (spawn the next on completion) or use `isolation: "worktree"`. Related: [[feedback_verify_via_harness]], [[feedback_finish_what_you_start]].
