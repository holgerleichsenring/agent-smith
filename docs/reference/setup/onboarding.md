# Onboarding: First-Run Bootstrap

!!! note "Which surface reads this"
    The YAML on this page is the file format. On a server the same values live in the database and are edited in the [Config studio](../../configure-it/config-studio.md); the CLI reads them from `agentsmith.yml`. `agent-smith config import` moves one into the other. See [Where configuration lives](../../configure-it/index.md).

Before Agent Smith can run a code-touching pipeline (`code`, `security-scan`, `api-security-scan`) against a repository, the repo needs two files per component it holds:

- `.agentsmith/contexts/<name>/context.yaml` — the component's fingerprint: stack, toolchain image, how it is verified. See [The context file](../concepts/context-file.md).
- `.agentsmith/contexts/<name>/principles.md` — the constraints Agent Smith respects when changing code.

The `init-project` pipeline produces them. Running it once per repository is the prerequisite. There are two ways to start it: the **Initialize** button on a project in the dashboard, or a labelled issue. Either way Agent Smith opens a PR with the generated files.

> Works the same way on GitHub, GitLab, Azure DevOps, and Jira. Webhook-based or polling-based — both paths feed the same trigger and route via `pipeline_from_label`.

## From the dashboard

Every project card in the Config studio (and the project panel under System) has an **Initialize** button. It starts `init-project` for that project with no ticket; the run is stamped as a manual one. While it runs the button turns into **Initializing — view run**, which opens the live run, and pressing it again never starts a second one. Pressing it on a repository that is already initialized is safe: a re-run that changes nothing opens no PR. It needs the `operator` role (see [Access control](../security/access-control.md)).

Next to it sits **Auto-accept PRs**, on by default. With it on, the run completes the init pull requests it opened, one repo at a time. The outcome per repo is one of three:

- merged;
- armed: on Azure Repos, a PR behind a required-build policy is approved and set to auto-complete, and merges itself once the build passes;
- refused: a branch policy, a required reviewer or a failing build said no. The PR stays open with the reason recorded for that repo, and the run does not fail over it.

Untick it when you want to review the generated files yourself.

The rest of this page walks the ticket-labelled path, which works the same on GitHub, GitLab, Azure DevOps and Jira.

## Prerequisites

- Agent Smith deployed and reachable from your platform (or polling enabled — see [Trigger: polling](../../trigger-it/polling.md)).
- The target repository connected as a project in `agentsmith.yml` (catalog-first shape: a `repos:`/`connections:` entry, a `trackers:` entry, a `projects:` entry wiring them). See the [agentsmith.yml reference](../configuration/agentsmith-yml.md).
- A trigger config (`github_trigger` / `gitlab_trigger` / `azuredevops_trigger` / `jira_trigger`) for that project, with `pipeline_from_label` containing `agent-smith:init: init-project`. This entry is already in the bundled `agentsmith.yml` example. See [Trigger: labels](../../trigger-it/labels.md) for the config shape.

## Step 1 — Verify the trigger config

Your project's trigger block must map the init label. Example for GitHub:

```yaml
repos:
  my-new-repo:
    type: github
    url: https://github.com/mycompany/my-new-repo
    auth: github_token

trackers:
  my-issues:
    type: github
    url: https://github.com/mycompany/my-new-repo
    auth: github_token

projects:
  my-new-repo:
    agent: claude-default
    tracker: my-issues
    repos: [my-new-repo]
    github_trigger:
      project_resolution:
        strategy: repo
        value: https://github.com/mycompany/my-new-repo.git
      pipeline_from_label:
        agent-smith:init: init-project    # the onboarding mapping
        bug: code
        feature: code
      done_status: "closed"
```

Place `agent-smith:init` **first** in `pipeline_from_label` — match order is dict-insertion order. Putting it first keeps onboarding visible to operators reading the config.

A server picks up the change without a restart.

## Step 2 — Create the init issue

In the platform UI:

1. Create an issue on the target repository. Title: `Initialize agent-smith` (or anything — the title is informational only).
2. Apply the label `agent-smith:init`.

That's the entire trigger. Agent Smith picks it up via webhook delivery (sub-second) or the next polling tick (default 60 s, see [Trigger: polling](../../trigger-it/polling.md)).

## Step 3 — Wait for the bootstrap PR

Agent Smith runs the `init-project` pipeline:

1. Checks out a fresh branch on each repository and analyzes it.
2. Discovers the repository's components. A component is something built and shipped on its own; the internal layer projects of one solution (a `Domain` or `Infrastructure` project, say) belong to the component that ships them and get no context of their own.
3. Runs one bootstrap round per component, on the agent's `context_generation` model. The round reads that component's subtree through tools and writes its `context.yaml`, including `stack.image` and a `verify` block derived from the CI pipeline the repository already runs.
4. Transfers the coding principles into each `principles.md`: the universal core, the delta for the component's language, and every framework overlay whose declared dependency is found at the component root (Spark via `org.apache.spark`, `pyspark` or `databricks-connect`), from the skills catalog. An existing `principles.md` is left untouched unless the init was launched with **Refresh principles**, which recomposes it and keeps its **Project Specifics** section verbatim.
5. Commits with message `chore: initialize .agentsmith/ directory` and opens a PR per repo (cross-linked when there are several).
6. With auto-accept on, completes the PRs as described above; on the ticket path it comments on the init issue with the PR link and transitions it to `done_status`.

Typical wall-clock: a few minutes, depending on repository size and the number of components.

## Step 4 — Review and merge

The PR body says, per context, what the run did with the principles (transferred from the core and language delta plus any overlays it names, refreshed with or without Project Specifics carried over, already present and left untouched, or authored by the skill) and, per file the language delta ships, whether it was written, already present, or not written and why. Review the files like any other PR:

- **`context.yaml`** — confirm the stack, the image and the `verify` stages match your repo.
- **`principles.md`** — the constraints Agent Smith will follow on subsequent runs. Loosen or tighten as appropriate for your team's conventions. The rules of your **environment** belong in it too, appended under its **Project Specifics** section — "field changes go through the estate's own CLI", "hand-written SQL in a model is a defect". That section survives every re-init, a refresh included, and merging this PR is where you ratify it.

Edit either file in the PR before merging. Agent Smith respects whatever lands on the default branch, not the initially-generated content.

Merge the PR. The repo is now bootstrapped.

## Step 5 — File follow-up tickets

The first real ticket can now run. Apply any other trigger label (`bug` → `code`, `feature` → `code`, `security-review` → `security-scan`, etc.) to a fresh issue, and the corresponding pipeline runs against the bootstrapped repository.

## Triggering init via Slack (optional)

If your deployment includes the Slack integration, you can also trigger `init-project` from a Slack channel — no ticket required:

- Modal: `/agent-smith` → **Init Project** → select repository.
- Chat: type `init my-project-name` in any channel where Agent Smith is present.

The Slack path produces the same bootstrap PR but does not transition any ticket (there's no ticket to transition). The label-triggered path is the supported flow for headless / k8s deployments without Slack.

## Troubleshooting

### My non-init pipeline says "Run init-project first"

Expected if you haven't merged the bootstrap PR yet. The BootstrapGate guards code-touching pipelines and aborts fast, naming the context and the file, when a `context.yaml` or `principles.md` is missing. A repository that still carries the old file name `coding-principles.md` is refused the same way; re-running `init-project` renames it to `principles.md` and keeps its Project Specifics. Merge the init PR first, then re-trigger.

### The init issue stayed open with no PR

Three likely causes:

1. **Label mismatch.** Verify the label string in `pipeline_from_label` exactly matches the label on the issue (case-sensitive on most platforms). The bundled config uses `agent-smith:init`; if you customized it, check that.
2. **`trigger_statuses` excludes the issue's state.** If your trigger config sets `trigger_statuses: ["open"]` but the issue was created in a different state, the trigger silently skips. Either widen `trigger_statuses` or change the issue's state.
3. **Agent Smith never received the event.** For webhooks: check the platform's webhook-delivery log. For polling: check Agent Smith's polling logs for the project name — the poller logs each tick.

### Bootstrap PR opened but the files look wrong

Edit them in the PR before merging — see Step 4. Or re-run `init-project`: it derives every context again instead of returning what is declared, so a wrong value (a wrong `meta.workdir`, a wrong image) gets corrected. Sections of `context.yaml` the generator does not model, like hand-written `decisions` or `integrations`, are carried over, and an existing `principles.md` is left as it is. A context the new derivation no longer produces is moved to `.agentsmith/contexts-retired/<name>/` and the move is part of the PR.

For language-detection misclassification specifically (e.g. a TypeScript monorepo bootstrapped as `generic`), check the [Bootstrap Skills](../skills/bootstrap.md) reference for the project_language enum and the per-language activation criteria.

### Where do I read the result.md?

Each agent run produces a `result.md` under `.agentsmith/runs/<run>/`. The init run's result.md surfaces the bootstrap-skill output, cost breakdown, and any warnings. Failed runs leave the same artifact path with the failure details — useful when the PR doesn't appear.

## Bootstrapping a multi-repo project

A multi-repo project (one project entry referencing N entries in `repos:` or a discovery glob) needs each repo to end up with its own contexts. One `agent-smith:init` ticket on the project, or one press of **Initialize**, does it: the `init-project` pipeline iterates the project's repos, writes the contexts into each, and opens one bootstrap PR per repo, cross-linked.

Re-running init later is safe: it derives again (see Troubleshooting above), and a re-init that produces no changes closes its ticket instead of looping. Every repo must be bootstrapped before ticket-triggered runs against the project succeed end-to-end (the `BootstrapGate` aborts code-touching pipelines on any repo missing its context files).

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
    pipeline: code
    jira_trigger:
      assignee_name: "Agent Smith"
      project_resolution: { strategy: tag, value: acme-product }
      pipeline_from_label:
        agent-smith:init: init-project
        bug: code
      default_pipeline: code
```

### Operator workflow

1. On `acme-backend`, file an issue (any title), apply the `agent-smith:init` label. Wait for the bootstrap PR (typically 1-3 minutes), review the generated `.agentsmith/contexts/` files, merge.
2. Repeat on `acme-frontend`.
3. Repeat on `acme-sdk`.

The three runs do not coordinate — each one detects its own repo's stack, writes its own files, and opens its own PR. You can run all three in parallel by labelling all three repos at once if you prefer; the queue will serialise them according to `agent.queue.max_parallel_jobs`.

Once every repo has the `.agentsmith/` directory merged on its default branch, subsequent ticket triggers against the project (e.g. a `bug`-labelled Jira issue) fan out to all three repos and execute the `code` pipeline end-to-end against each.

> **Pitfall**: a ticket on a partially-bootstrapped multi-repo project still spawns N pipeline runs. The runs against bootstrapped repos succeed; the runs against not-yet-bootstrapped repos abort fast with "Run init-project first" and produce a failed-run artefact under `.agentsmith/runs/<run-id>/`. This is noisy. Bootstrap every repo in the project before relying on ticket-triggered runs.

See [Repos: multi-repo](../../connect-your-stuff/repos-multi.md) for the multi-repo project model.

## See also

- [Trigger: labels](../../trigger-it/labels.md) — full reference for `pipeline_from_label` config and matching rules.
- [Ticket Lifecycle](../concepts/ticket-lifecycle.md) — how Agent Smith claims tickets and transitions their state.
- [agentsmith.yml Reference](../configuration/agentsmith-yml.md) — complete config schema.
- [Repos: multi-repo](../../connect-your-stuff/repos-multi.md) — fan-out behaviour and the parallel-isolation model.
- [Project Resolution Strategies](../configuration/project-resolution.md) — `tag`, `area-path`, `repo`, `to_address`.
