---
name: feedback_finish_what_you_start
description: "Build to completion + run all tests + smoke all deployment targets before declaring a task done"
metadata:
  type: feedback
status: proposed
---
A task is not done until: `dotnet build` is 0 warnings/0 errors AND `dotnet test` is fully green AND the affected pipelines have been smoked end-to-end on the affected deployment targets — CLI binary, docker-compose, k8s manifests where applicable. Minimum smoke set: `api-scan`, `security-scan`, `fix-bug` / `add-feature` — these are the three customer-facing flows. Any other affected flow is pulled along.

**Why:** The operator has had to do far too much fix-up after my "I'm done" claims. Sometimes the build was warning-free but a deployment target was broken; sometimes tests passed but the actual CLI didn't start; sometimes a phase landed half-implemented because I stopped at the first green signal. Each fix-up round costs him concrete time and erodes trust in my output. Concrete incidents: the p0146/p0147 parallel wave that merged into a non-building main (60+ errors) because individual agent PRs went green in isolation but the merged result wasn't validated; the Drop-2 attempt where I built a regex extractor for the wrong layer because I stopped at "the test fixture passes" without questioning the design premise.

**How to apply:**

1. After implementation, run `dotnet build` and require 0 warnings + 0 errors. Treat warnings as failures unless explicitly waived by the operator.
2. Run the full test suite with `dotnet test`. 0 failed.
3. Sanity-run the CLI binary for the affected pipelines. If the change touches the API-security path, do an `api-scan` against the operator's standing smoke target. Same for `security-scan` and `fix-bug`.
4. If docker-compose configs or k8s manifests were touched, validate they still apply (k8s `kubectl apply --dry-run=client` minimum; docker-compose `up` in a scratch namespace if practical).
5. Don't push or open a PR until all of the above is done. "Tests green" alone is not a finished task.
6. Don't issue a parallel agent wave whose merged result hasn't been mentally pre-validated for collisions on shared files. If two phases touch the same class, they don't run in parallel — they run sequentially.
