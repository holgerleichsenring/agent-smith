# Capacity, queueing and cancel

What happens when more runs arrive than your host or cluster can carry. Short version: nothing fails — runs queue in arrival order, and the numbers you see are the numbers you pay for.

## Admission: check before you claim

Before a triggered ticket is even claimed, the spawner asks the capacity probe one question: does this run's *whole footprint* fit right now? The footprint is honest — one orchestrator pod plus one sandbox per repo the run will touch (p0320b). No fit means the run is recorded as **queued**, without claiming the ticket and without spawning anything that would then die.

Per backend:

- **Docker** (compose / single host): a proactive cap, `max_concurrent_sandboxes`, counted against the labelled sandbox containers actually running. A real out-of-memory on the host still fails honestly — the cap exists so you stop before that point.
- **Kubernetes**: the probe reads the namespace `ResourceQuota` and compares against the keys present in its `status.hard`. Quota exceeded is a structured queue event, never a crash-looping pod. The quota shape you want (requests-based, plus a `pods` cap) is worked through on the [Kubernetes host page](../../host-it/kubernetes.md#capacity-quota-count-requests-not-limits).

## The queue: strict FIFO, one entry per ticket

Queued runs go into a persistent FIFO (p0320c). The properties that matter:

- **One entry per ticket.** A ticket that triggers while queued does not multiply into N run rows.
- **Strict arrival order.** Nothing overtakes; a small run does not jump a big one.
- **A server-side pump** launches the head of the queue the moment capacity frees — no polling delay, no operator action.
- **Re-launch reuses the run row.** The queued run and the eventually-executed run are the same run in the history, not a failed one plus a mystery duplicate.

Resumed runs (a run that checkpointed on a question and got its answer — see [durable dialogue](../../how-it-works/expectations.md)) ride the same queue with the same rules.

In the [dashboard](dashboard.md) a queued run is amber, shows "queued · #position" and the reason it's waiting, and there's a filter chip for them. Position also comes back on `/api/runs`.

## Sizing: pipeline-aware, clamped

What a sandbox asks for is not one global number (p0320a):

- **Code-changing pipelines** (fix-bug, add-feature, phase execution) use the repo's declared `stack.resources` from its `.agentsmith/context.yaml` — the LLM proposes them during init, you can edit them, and the framework clamps them to a hard ceiling either way.
- **Non-build pipelines** (init-project, scans, legal analysis, mad-discussion) get a light fixed profile. A security scan reads code; it doesn't need a build box.
- The spawned orchestrator pod is sized separately and small (it runs the LLM loop, compiles nothing) — see the env values in `deploy/k8s/8-deployment-server.yaml`.

And before sizing even matters, the `ScopeRepos` step (p0331) narrows the run to the repos the ticket actually touches, so a five-repo project doesn't provision five sandboxes for a one-repo fix. If the master discovers mid-run it needs another repo after all, it has an `ensure_repo_sandbox` tool to escalate — the widening is recorded as a scope decision on the run.

## What a run costs, honestly

Every finished run shows two costs side by side (p0332):

- **LLM cost** — tokens and dollars, per call, with the cached share (see [cost tracking](../concepts/cost-tracking.md)).
- **Reserved capacity-time** — memory request × pod lifetime, in Gi·minutes, summed over the run's pods. Reservation, not measured usage: it's what your cluster had to hold free for the run, which is what capacity planning and cloud bills are made of.

A run can be cheap in tokens and expensive in pods (a big build that thinks little) or the reverse. Now you can see which.

## Cancel is a state, not a wish

Cancelling a run (dashboard button or `POST /api/runs/{runId}/cancel`) writes a persistent cancel state that is enforced everywhere (p0330):

- A run that hasn't started yet is cancelled before it ever spawns.
- A running run gets a graceful window (30 seconds), then a server-side force-kill that tears down its pods — compute and capacity are released immediately, and the ticket is terminalized on the tracker.
- Cancelled is its own terminal status in the history, distinct from failed.

## The knobs, in one place

| Knob | Where | What it bounds |
|---|---|---|
| `queue.MaxParallelJobs` | `agentsmith.yml` (server) | Concurrent runs the consumer will execute (default 4). |
| `max_concurrent_sandboxes` | Docker backend | Sandbox containers on one host. |
| `ResourceQuota` (`requests.cpu` / `requests.memory` / `pods`) | your namespace | Whole-cluster footprint; the `pods` key is the deterministic backpressure knob. |
| `stack.resources` | per-repo `context.yaml` | Build-sandbox size for code-changing pipelines (clamped). |
| `projects.X.sandbox.resources` | `agentsmith.yml` | Per-project sandbox sizing override. |
| `pipeline_cost_cap` | `agentsmith.yml` | USD / token budget per run — the other half of "bounded". |

## Next

- [Kubernetes host page](../../host-it/kubernetes.md) — the quota worked example.
- [Dashboard](dashboard.md) — where queued/cancelled/waiting states are visible.
- [Cost tracking](../concepts/cost-tracking.md) — the token side of the bill.
