---
name: feedback_safety_in_api_not_process
description: "For dangerous operations the safety belongs in the API/type-system, not in operator caution or hand-written workaround comments — caution doesn't scale with LLM-speed work"
metadata:
  type: feedback
status: proposed
---
When a code path has a dangerous default (recursive delete, force-push, schema drop, network broadcast), the right fix is to remove the default and force every caller to express intent — not to rely on operator caution or warning comments.

**Why:** During the 2026-05-27 sandbox-deleted-checkout incident, operator observation: process-vigilance ("we will simply be more careful") doesn't scale to LLM-speed development. The real defect was `InProcessSandbox.DisposeAsync` always running `Directory.Delete(workDir, recursive: true)` — trust in self. Two CLI command sites had even left explicit warning comments ("kein await using weil sonst rm -rf des User-Pfads") instead of fixing the API. Those comments were worthless the moment the sandbox flowed through DI scope. Operator quote: "the autonomy of what we produce is the problem — not you or me." (translated)

**How to apply:**
- When refactoring a dangerous operation: prefer constructor-required flags with no default over documented danger. `InProcessSandbox(..., bool ownsWorkDir, ...)` with no default = every call site is forced to spell out intent and the compiler enforces it.
- When you see a "don't use feature X because it would delete Y" comment in code, treat that as a bug report against the API design, not a usage instruction.
- When proposing fixes for dangerous-default bugs, add a one-line static-analysis or audit-test rule alongside the patch (e.g. "every `Directory.Delete(recursive:true)` on a non-locally-allocated path must be allowlisted") — that closes the regression door for future commits.
- Related: [[feedback_no_wrapper_shims]] (don't paper over with adapters), [[feedback_finish_what_you_start]] (the fix isn't done until the dangerous default is gone, not just documented).
