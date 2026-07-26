---
name: feedback_code_quality_bar
description: "Reference quality is the operator's internal .NET reference codebases — Installer pattern, fluent step-builders, transient default, no phase-history comments"
metadata:
  type: feedback
status: proposed
---
Operator's quality bar is the internal .NET reference codebases (background worker + workflow processor). Concrete reference points (paths anonymized — see [[feedback_no_customer_names]]):

- **DI:** `<BackgroundWorker>/Installers/` — `InstallerBase` pattern, one Installer per subdomain (~15 lines each), composition root has a `List<IInstaller>` + foreach. NOT one fat `ServiceCollectionExtensions.cs` with helper methods. The fat file looks split but isn't — parallel work blows it apart because the registrations still share one method's call list.
- **Pipelines:** `<WorkflowProcessor>/Services/Pipelines/<StatePipelineBuilder>.cs` — fluent builder with `pipelineStepFactory.CreateStepContext<TContext>(User, State)` + per-step `(_) => predicate`. Steps are data + condition, not branching code. That's what makes pipelines extensible without merge pain.

**Rules I keep failing on:**

- **No phase-history comments in code** (`// p0123: ...`, `// p0140d: ...`). Belongs in the commit message, the phase YAML, or the per-phase decision file — not the source. Rot inevitable.
- **No `new ServiceX(logger, factory, ...)` outside test builders.** If a class needs `new` with logger injection, it should be in DI. Caught me doing this multiple times in 2026-05.
- **Transient is the default lifetime.** Singleton requires a documented reason in the decision file — usually it's not actually correct. "Stateless" is not a reason to be Singleton; it's a reason it doesn't matter, but if scope mismatches surface later, Transient avoids the trap.
- **Class names must point at one responsibility.** `*Manager`, `*Helper`, `*Coordinator` are smells. If the name needs an "And" (`PipelineExecutor` doing iteration AND error handling AND sandbox AND cost AND lifecycle), it's not modeled yet.
- **Don't keep legacy alive just because a test exists.** When a class becomes dead, its test goes too. Code decay through defensive test-shielding is worse than no test.
- **DI for every dependency, no exceptions outside test builders.** No ctor arguments that are not registered services. No `new ServiceX(logger, ...)` in production code. Same principle as the manual-new rule above, said the other way around: if a class needs it, it gets it through DI.
- **Tell don't ask.** Don't write `if (obj.HasX()) { do thing with obj.X }`; write `obj.DoThing()`. Move the decision inside the object that owns the data. Caller asks for a verb, not for state.

**How to apply:** Before claiming a refactor "done", reread the diff with the InstallerBase / fluent-builder lens. If the new shape would still produce a 500-line god-class on merge, it's not actually a refactor — it's a rearrangement.
