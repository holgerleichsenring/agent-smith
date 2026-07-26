---
name: feedback_no_wrapper_shims
description: "The operator rejects wrapper/adapter classes that exist only as migration bridges or to keep legacy interfaces alive — migrate every caller directly instead"
metadata:
  type: feedback
status: proposed
---
When migrating from one interface/abstraction to another (e.g. ILlmClient → IChatClient), do NOT introduce a wrapper class that fakes the old interface on top of the new one (e.g. `ChatClientLlmAdapter : ILlmClient` wrapping IChatClient). Even if it would keep ~20 caller sites working without changes, the shim itself is a code smell that accumulates complexity without paying it back. Same for decorators that exist purely to bridge a legacy concern to new infrastructure.

**Why:** During p0119 → p0119a planning, I twice proposed a `ChatClientLlmAdapter` (once on my own, once after agreeing with the user that "no wrapper" was the rule and then drifting back). The operator pushed back both times: shims fake the migration, hide the real cost, and leave a half-translated codebase. Cleaner to rip the bandaid — every caller takes the new factory in its ctor and calls the new API directly. If that means touching 20 files instead of 1, that's the honest scope.

**How to apply:** When a phase migrates an interface, the spec must list every caller as a `modify:` entry, not introduce a bridge class. Cost-tracking decorators (e.g. TrackingLlmClient feeding PipelineCostTracker transparently) also fall under this — replace with explicit per-caller calls. The verbose explicit pattern is preferred over the elegant hidden shim. M.E.AI middleware composition (`.AsBuilder().Use(...)`) is fine when it adds a real cross-cutting concern at the IChatClient layer, but introducing a class purely to translate one interface to another is what gets rejected.
