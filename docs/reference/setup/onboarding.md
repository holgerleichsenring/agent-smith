# Onboarding: First-Run Bootstrap

Before Agent Smith can run a code-touching pipeline (`fix-bug`, `add-feature`, `security-scan`, `api-security-scan`, `autonomous`) against a repository, the repo needs two files:

- `.agentsmith/context.yaml` — the project's architectural fingerprint (stack, modules, conventions).
- `.agentsmith/coding-principles.md` — the constraints Agent Smith respects when changing code.

The `init-project` pipeline produces both files. Running it once per repository is the prerequisite. This guide walks the recommended onboarding flow — a labelled issue triggers `init-project`, Agent Smith opens a PR with the generated files, and the operator reviews and merges. Subsequent agent-smith labels then work normally.

> Works the same way on GitHub, GitLab, Azure DevOps, and Jira. Webhook-based or polling-based — both paths feed the same trigger and route via `pipeline_from_label`.

## Prerequisites

- Agent Smith deployed and reachable from your platform (or polling enabled — see [Polling Setup](polling.md)).
- The target repository connected as a project in `agentsmith.yml`. Minimum: `source` and `tickets` blocks for the platform. See the [agentsmith.yml reference](../configuration/agentsmith-yml.md).
- A trigger config (`github_trigger` / `gitlab_trigger` / `azuredevops_trigger` / `jira_trigger`) for that project, with `pipeline_from_label` containing `agent-smith:init: init-project`. This entry is already in the bundled `agentsmith.yml` example. See [Label-Based Triggers](label-triggers.md) for the config shape.

## Step 1 — Verify the trigger config

Your project's trigger block must map the init label. Example for GitHub:

```yaml
projects:
  my-new-repo:
    source:
      type: GitHub
      url: https://github.com/mycompany/my-new-repo
      auth: token
    tickets:
      type: GitHub
      url: https://github.com/mycompany/my-new-repo
      auth: token
    github_trigger:
      pipeline_from_label:
        agent-smith:init: init-project    # the onboarding mapping
        bug: fix-bug
        feature: add-feature
      default_pipeline: fix-bug
      done_status: "closed"
```

Place `agent-smith:init` **first** in `pipeline_from_label` — match order is dict-insertion order. Putting it first keeps onboarding visible to operators reading the config.

Restart Agent Smith if it was already running (config changes are not hot-reloaded).

## Step 2 — Create the init issue

In the platform UI:

1. Create an issue on the target repository. Title: `Initialize agent-smith` (or anything — the title is informational only).
2. Apply the label `agent-smith:init`.

That's the entire trigger. Agent Smith picks it up via webhook delivery (sub-second) or the next polling tick (default 60 s, see [Polling vs Webhooks](polling-vs-webhooks.md)).

## Step 3 — Wait for the bootstrap PR

Agent Smith runs the `init-project` pipeline:

1. Checks out a fresh branch (`agentsmith/init`) on the repository.
2. Analyzes the project to detect its primary language (csharp / node / python / generic).
3. Dispatches the language-specific bootstrap skill that writes `.agentsmith/context.yaml` + `coding-principles.md`.
4. Commits with message `chore: initialize .agentsmith/ directory` and opens a PR.
5. Comments on the init issue with the PR link, then transitions it to `done_status` (or closes it if no `done_status` is configured).

Typical wall-clock: 1–3 minutes depending on repository size and the language detector's depth.

## Step 4 — Review and merge

The PR contains exactly two files. Review them like any other PR:

- **`context.yaml`** — confirm the stack, modules, and conventions match your repo. Generic-bootstrap output is deliberately minimal and flagged as a fallback; you may want to flesh it out before merging.
- **`coding-principles.md`** — the constraints Agent Smith will follow on subsequent runs. Loosen or tighten as appropriate for your team's conventions.

Edit either file in the PR before merging — Agent Smith respects whatever lands on the default branch, not the initially-generated content. If the output is wrong shape or the generator misclassified your stack, edit `.agentsmith/context.yaml` directly in the PR; you don't need to re-run `init-project`.

Merge the PR. The repo is now bootstrapped.

## Step 5 — File follow-up tickets

The first real ticket can now run. Apply any other trigger label (`bug` → `fix-bug`, `feature` → `add-feature`, `security-review` → `security-scan`, etc.) to a fresh issue, and the corresponding pipeline runs against the bootstrapped repository.

## Triggering init via Slack (optional)

If your deployment includes the Slack integration, you can also trigger `init-project` from a Slack channel — no ticket required:

- Modal: `/agent-smith` → **Init Project** → select repository.
- Chat: type `init my-project-name` in any channel where Agent Smith is present.

The Slack path produces the same bootstrap PR but does not transition any ticket (there's no ticket to transition). The label-triggered path is the supported flow for headless / k8s deployments without Slack.

## Troubleshooting

### My non-init pipeline says "Run init-project first"

Expected if you haven't merged the bootstrap PR yet. The BootstrapGate guards code-touching pipelines and aborts fast when either `.agentsmith/context.yaml` or `coding-principles.md` is missing. Merge the init PR first, then re-trigger.

### The init issue stayed open with no PR

Three likely causes:

1. **Label mismatch.** Verify the label string in `pipeline_from_label` exactly matches the label on the issue (case-sensitive on most platforms). The bundled config uses `agent-smith:init`; if you customized it, check that.
2. **`trigger_statuses` excludes the issue's state.** If your trigger config sets `trigger_statuses: ["open"]` but the issue was created in a different state, the trigger silently skips. Either widen `trigger_statuses` or change the issue's state.
3. **Agent Smith never received the event.** For webhooks: check the platform's webhook-delivery log. For polling: check Agent Smith's polling logs for the project name — the poller logs each tick.

### Bootstrap PR opened but the files look wrong

Edit them in the PR before merging — see Step 4. The bootstrap skills produce a reasonable starting point but aren't omniscient about your conventions. Subsequent `init-project` runs would just overwrite anything that's not under `.agentsmith/`, so re-running isn't usually the right fix.

For language-detection misclassification specifically (e.g. a TypeScript monorepo bootstrapped as `generic`), check the [Bootstrap Skills](../skills/bootstrap.md) reference for the project_language enum and the per-language activation criteria.

### Where do I read the result.md?

Each agent run produces a `result.md` under `.agentsmith/runs/<run-id>/`. The init run's result.md surfaces the bootstrap-skill output, cost breakdown, and any warnings. Failed runs leave the same artifact path with the failure details — useful when the PR doesn't appear.

## Bootstrapping a multi-repo project

A multi-repo project (one project entry referencing N entries in `repos:`) needs `agent-smith:init` to run **once per repo** — each repo needs its own `.agentsmith/context.yaml` and `.agentsmith/coding-principles.md`. There is no project-wide init.

The init runs are independent. You can do them sequentially or label all repos at once; ordering does not matter. The only constraint is that every repo must be bootstrapped before ticket-triggered runs against the project will succeed end-to-end (the `BootstrapGate` aborts code-touching pipelines on any repo that's missing the two files).

### Example: a 3-repo project

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
      project_resolution: { strategy: tag, value: acme-product }
      pipeline_from_label:
        agent-smith:init: init-project
        bug: fix-bug
      default_pipeline: fix-bug
```

### Operator workflow

1. On `acme-backend`, file an issue (any title), apply the `agent-smith:init` label. Wait for the bootstrap PR (typically 1-3 minutes), review the generated `.agentsmith/context.yaml` and `coding-principles.md`, merge.
2. Repeat on `acme-frontend`.
3. Repeat on `acme-sdk`.

The three runs do not coordinate — each one detects its own repo's stack, writes its own files, and opens its own PR. You can run all three in parallel by labelling all three repos at once if you prefer; the queue will serialise them according to `agent.queue.max_parallel_jobs`.

Once every repo has the `.agentsmith/` directory merged on its default branch, subsequent ticket triggers against the project (e.g. a `bug`-labelled Jira issue) fan out to all three repos and execute the `fix-bug` pipeline end-to-end against each.

> **Pitfall**: a ticket on a partially-bootstrapped multi-repo project still spawns N pipeline runs. The runs against bootstrapped repos succeed; the runs against not-yet-bootstrapped repos abort fast with "Run init-project first" and produce a failed-run artefact under `.agentsmith/runs/<run-id>/`. This is noisy. Bootstrap every repo in the project before relying on ticket-triggered runs.

See [Multi-Repo Projects](../configuration/multi-repo.md) for the fan-out model, ambiguous-tag handling, and the metrics that quantify cost-of-ambiguity in multi-repo setups.

## See also

- [Label-Based Triggers](label-triggers.md) — full reference for `pipeline_from_label` config and matching rules.
- [Ticket Lifecycle](../concepts/ticket-lifecycle.md) — how Agent Smith claims tickets and transitions their state.
- [agentsmith.yml Reference](../configuration/agentsmith-yml.md) — complete config schema.
- [Multi-Repo Projects](../configuration/multi-repo.md) — fan-out behaviour and the parallel-isolation model.
- [Project Resolution Strategies](../configuration/project-resolution.md) — `tag`, `area-path`, `repo`, `to_address`.
