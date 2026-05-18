# Multi-Repo Projects

A project with more than one repo runs N pipelines per ticket — one per repo, in parallel, each completely isolated. This page explains when to model your work that way, what an operator sees on a multi-repo run, and what Agent Smith deliberately does *not* do across repos.

> Multi-repo execution is the p0140 umbrella feature (slices a-e). The catalog plumbing landed in p0139; spawn fan-out, per-repo writeback, and the counters that quantify it landed in p0140a-e.

## When to use multi-repo

Use a multi-repo project when a single ticket source represents work that spans multiple code repositories, and every repo should react to the same ticket independently.

The motivating shape: a parent project owns N repos that share one tracker. Examples:

- **One Jira project for a product split across `backend`, `frontend`, and `sdk` repos.** A ticket on the Jira project should produce three PRs — one against each repo — when the change affects all three. (When the change only affects one, the other two repos' Plan phases will come back empty and the runs skip out gracefully — see [Ambiguous-tag handling](#ambiguous-tag-handling) below.)
- **One Azure DevOps project containing the work-item backlog for a multi-service deployment**, where each service lives in its own Azure Repo. The work item is filed once; Agent Smith fans out to each service repo.
- **A monorepo migration in progress**, where the old polyrepo set still exists and you want both modernised and legacy paths exercised by the same ticket.

Use single-repo (one entry in `repos:`) when there is exactly one repo per ticket source. That is the common case; most projects never need multi-repo.

## YAML schema

A multi-repo project lists multiple catalog entries under `projects.<name>.repos:`. Each name refers to a [repo catalog entry](agentsmith-yml-schema.md#repos--source-repository-catalog).

```yaml
repos:
  acme-backend:
    type: GitHub
    url: https://github.com/acme/backend
    auth: github_token
  acme-frontend:
    type: GitHub
    url: https://github.com/acme/frontend
    auth: github_token
  acme-sdk:
    type: GitHub
    url: https://github.com/acme/sdk
    auth: github_token

trackers:
  acme-jira:
    type: Jira
    url: https://acme.atlassian.net/
    auth: jira_token
    open_states: ["To Do", "In Progress"]
    done_status: "In Review"

agents:
  claude-default:
    type: Claude
    model: claude-sonnet-4-20250514

projects:
  acme-product:
    agent: claude-default
    tracker: acme-jira
    repos:
      - acme-backend
      - acme-frontend
      - acme-sdk
    pipeline: fix-bug
    jira_trigger:
      assignee_name: "Agent Smith"
      project_resolution:
        strategy: tag
        value: acme-product
      pipeline_from_label:
        bug: fix-bug
        feature: add-feature
      default_pipeline: fix-bug
```

The `project_resolution` block is what tells Agent Smith *which* project owns an incoming ticket on the shared `acme-jira` tracker. See [Project Resolution Strategies](project-resolution.md) for the full reference; in the example above, any Jira issue tagged `acme-product` is claimed by this project and fans out to all three repos.

## What the operator sees on a multi-repo run

A single ticket produces:

- **N parallel pipeline runs**, one per repo. Each run has its own sandbox directory, its own working branch, its own diff, and its own LLM context (each run reads only the `.agentsmith/context.yaml` and `coding-principles.md` from its own repo).
- **N pull requests** — one against each repo. They are independent PRs at the platform level; reviewing or merging one does not affect the others.
- **N writeback comments on the ticket**, posted by the `CommitAndPRCommand`. Each comment is headlined with the repo name so you can tell them apart:

  ```
  ## Agent Smith - Completed in acme-backend
  PR: https://github.com/acme/backend/pull/482
  ...

  ## Agent Smith - Completed in acme-frontend
  PR: https://github.com/acme/frontend/pull/319
  ...

  ## Agent Smith - Completed in acme-sdk
  PR: https://github.com/acme/sdk/pull/77
  ...
  ```

  The same header convention applies to `InitCommit` for `agent-smith:init` runs.

The fan-out happens at claim time. Both webhooks and polling produce a single `IncomingTicketEnvelope`; `WebhookSpawnDispatcher` (webhooks) and the per-tracker poller (polling) call `SpawnPipelineRunsUseCase`, which enqueues one `PipelineRequest` per repo in the project. From that point on, each run executes independently through the standard pipeline machinery.

> **Pitfall**: a multi-repo project produces N entries in the queue per ticket. If you set `repos:` to ten entries with `agent.queue.max_parallel_jobs: 4`, the runs serialise. That is fine — it just takes longer. Size your queue concurrency for the largest fan-out you expect, not the smallest.

## Ambiguous-tag handling

Project resolution can match more than one project. This is intentional. Two scenarios:

1. **Two projects deliberately share the same tag.** For example, you might have two separate agent-smith projects (different agent configs, different repo sets) that both want to react to the `urgent-fix` tag. Both projects claim the ticket and spawn.
2. **A multi-repo project fans out to repos where only some are relevant.** All N runs start; the irrelevant ones come back with empty Plans (the LLM saw no actionable work in this repo for this ticket) and skip out gracefully.

In both cases, the design is: spawn all matched (project, pipeline) pairs, let each pipeline's Plan phase decide whether the work is genuinely relevant, and skip when the plan is empty.

Two counters in the [metrics surface](../operations/metrics.md) quantify this:

- `agent_smith_ambiguous_resolution_total{project,pipeline}` — incremented once per matched (project, pipeline) when the resolver returns more than one match.
- `agent_smith_pipeline_skipped_as_irrelevant_total{project,pipeline,reason="empty_plan"}` — incremented when the post-Plan gate sees a zero-step plan and signals a graceful skip.

The ratio `skipped_as_irrelevant / ambiguous_resolution` per (project, pipeline) is the cost-of-ambiguity dashboard signal. A high ratio (most ambiguous fan-outs ending in empty plans) means the tag is too broad and the LLM is doing free triage work that a tighter `project_resolution` would avoid. A low ratio means most ambiguous fan-outs do produce real work — the tag is correct, the fan-out is paying off.

See [metrics.md — Cost-of-ambiguity dashboard](../operations/metrics.md#cost-of-ambiguity-dashboard) for the PromQL.

## The explicit non-feature: no cross-repo coordination

Agent Smith deliberately does not coordinate across the repos of a multi-repo run. Concretely:

- There is no shared planning step that decides which repo gets which slice of the change.
- There is no cross-repo data flow at the spawner level. Each run's LLM reads only its own repo's `.agentsmith/context.yaml` and `coding-principles.md`. Run A does not see run B's diff or PR.
- There is no synchronisation point. Run A finishes (or fails, or times out) independently of run B.

If a change in `acme-backend` breaks `acme-sdk`'s contract, the spawner does not know about it. Two safety nets catch this:

1. **The `acme-sdk` run's own Verify phase** — its test suite runs against its own repo's main branch plus the in-flight diff. If the backend's PR has not merged yet, the sdk's tests run against the *old* backend contract and pass. If the backend PR *has* merged before the sdk run finishes, the sdk's tests run against the new contract and either pass or fail loudly.
2. **The operator on review.** Multi-repo PRs land in three separate review queues; the human reviewing each one is the cross-repo coordinator.

The reason cross-repo coordination is out of scope: it is hard, it is opinionated, and it leaks into questions of dependency-graph modelling that agent-smith does not currently solve. Operators who need it build it externally (a meta-PR, a release branch, a manual gate). The single-repo isolation model is the contract — multi-repo is parallel-N of that contract, not a distributed extension of it.

## Init flow for multi-repo projects

Each repo in a multi-repo project needs its own `.agentsmith/context.yaml` and `.agentsmith/coding-principles.md`. The `init-project` pipeline runs **once per repo** — it is not project-wide.

The workflow:

1. Pick one repo, e.g. `acme-backend`. File an issue, apply the `agent-smith:init` label, wait for the PR, review and merge.
2. Repeat for `acme-frontend`.
3. Repeat for `acme-sdk`.

The three init runs are independent and can overlap — there is no ordering requirement. Once all three repos have their `.agentsmith/` files merged on the default branch, subsequent ticket-triggered runs against the project will fan out and execute end-to-end.

See [Onboarding — Bootstrapping a multi-repo project](../setup/onboarding.md#bootstrapping-a-multi-repo-project) for the step-by-step.

> **Pitfall**: the `BootstrapGate` runs per pipeline run, against the run's own repo. If `acme-backend` is bootstrapped but `acme-sdk` is not, an `acme-product` ticket will fan out to all three repos; the sdk's run will abort fast with "Run init-project first". That abort does not affect the backend's run, which proceeds normally. Bootstrap all repos in a multi-repo project before relying on ticket-triggered runs.

## Related phases

- **p0140a** — Project resolver foundation. Introduced `project_resolution` on every trigger block, the `IncomingTicketEnvelope`, and the `ResolutionStrategy` enum.
- **p0140b** — Webhook handler migration. All eight ticket-event handlers route through `IEnvelopeProjectResolver` + `WebhookSpawnDispatcher` + `SpawnPipelineRunsUseCase`. `ClaimSpawnAsync` became the multi-repo claim region; `ClaimRequest` and `PipelineRequest` gained `RepoName`; zero-match envelopes produce a structured log and an opt-in tracker comment via `TrackerConnection.ZeroMatchComment`.
- **p0140c** — Per-tracker pollers. One `TrackerPoller` class replaced the four per-platform pollers. N projects sharing one tracker now do one poll per interval. Polling supports tag-strategy resolution only (the `Ticket` entity has Labels but not AreaPath / SourceRepoUrl).
- **p0140d** — Repo shim removal. `ResolvedProject.Repo` is gone; `ContextKeys.CurrentRepo` + `ExecutePipelineUseCase` resolve the `RepoName` on the request back to a `RepoConnection`. Twelve internal consumers migrated; multi-repo end-to-end execution is the result.
- **p0140e** — Metrics and docs (this slice). Introduced the `AgentSmithMeter` static class plus the two counters that quantify ambiguous resolution and empty-plan skips. See [metrics.md](../operations/metrics.md).

## See also

- [Project Resolution Strategies](project-resolution.md) — `tag`, `area-path`, `repo`, `to_address`.
- [Onboarding](../setup/onboarding.md) — bootstrapping repos; multi-repo section linked above.
- [Metrics](../operations/metrics.md) — the two p0140 counters and how to wire an exporter.
- [agentsmith.yml Schema](agentsmith-yml-schema.md) — `repos:`, `trackers:`, and `projects:` reference.
