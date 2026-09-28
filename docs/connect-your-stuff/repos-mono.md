# Repos: mono-repo

One product, one repo. The smallest viable Agent Smith setup. Use this page when your `TodoList` (or whatever it really is) lives in a single git repo.

## In the studio

Configuration for a server lives in the database and you edit it in the dashboard: switch the left rail to **Configuration** and work down the catalogs. The order matters, because each entry references the one before it: **Secrets** (names only), then **Agents**, then **Repositories**, then a **Tracker**, then a **Project** that wires them together. References are picked from dropdowns, so a project can't point at something that doesn't exist, and the drawer keeps **Create** disabled until every reference resolves.

For a single repo it is five entries: one secret, one agent, one repository, one tracker, and one project that ticks that repo.

The full tour is on [The Config studio](../configure-it/config-studio.md); what follows is the same wiring written as YAML, which is what the CLI reads directly and what `agent-smith config import` takes.

## The same thing as YAML


```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/holgerleichsenring/agent-smith/main/config/agentsmith.schema.json

agents:
  default-openai:
    type: openai
    models:
      scout:   { model: gpt-4.1-mini }
      primary: { model: gpt-4.1 }

repos:
  todolist:
    type: github
    url: https://github.com/acme-org/todolist
    auth: github_token

trackers:
  acme-issues:
    type: github
    url: https://github.com/acme-org/todolist     # the repo whose issues are the tickets
    auth: github_token

projects:
  todolist:
    agent: default-openai
    tracker: acme-issues
    repos: [todolist]                  # one entry → mono-repo

secrets:
  openai_api_key: ${OPENAI_API_KEY}
  github_token:   ${GITHUB_TOKEN}
```

That's it. The `repos:` list on `projects.todolist` has one entry, the project is a mono-repo, and Agent Smith spawns one sandbox for it per run (one per toolchain, if the repo holds contexts that need different ones). Skills need no configuration — they ship embedded in the release; a `skills:` block is only an override for skills development or air-gap mirrors (see [Skills catalog](../how-it-works/skills-catalog.md)).

The explicit `repos:` catalog entry shown here is the right shape for exactly this case — a single, known repo. It also stays available as the escape hatch for a repo that isn't discoverable through a provider API. The moment you have several repos under one org, prefer a `connections:` entry and let Agent Smith discover them — see [Repos: multi-repo](repos-multi.md).

## What happens at run-time

For a ticket targeted at the `todolist` project:

1. Agent Smith spawns one sandbox with the toolchain image for the repo's contexts, read from the `stack:` block of each `.agentsmith/contexts/<name>/context.yaml`, or a generic image when nothing names one.
2. The repo gets cloned into `/work` inside the sandbox.
3. A branch named `agent-smith/{ticket}` (for issue 54, `agent-smith/54`) is cut from the default branch.
4. The agent reads, writes and tests, all inside that sandbox.
5. The commit is pushed; one pull request is opened.
6. The ticket is updated with the PR URL.

The internal mechanics are the same as for multi-repo runs — there's just one sandbox in the dict instead of N.

## What changes if you grow a second repo

You add the new repo to the top-level `repos:` catalog and reference it from `projects.todolist.repos` — or, better, declare a `connections:` entry once (host, org, auth) and reference repos under it by glob, so new repos are discovered instead of hand-listed. The lifecycle code is identical either way; multi-repo just means the list has more than one entry. See [Repos: multi-repo](repos-multi.md) for the worked example.

The interesting bit: every repo in the project needs its own contexts, `.agentsmith/contexts/<name>/context.yaml` plus `.agentsmith/contexts/<name>/principles.md`, one directory per component the repo holds, so Agent Smith knows the toolchain and conventions for each. The `init-project` pipeline bootstraps them for every repo of the project; the **Initialize** button on the project card runs it. Do that once before the first `code` run. See [Context file](../reference/concepts/context-file.md) for what a context declares.

## What about pipelines, triggers, hosting?

Same as multi-repo. The workflow and the label map live on the tracker, and a project declares how tickets find it, regardless of whether it has one repo or fifteen. See:

- [Trigger it: webhooks](../trigger-it/webhooks.md)
- [Trigger it: labels](../trigger-it/labels.md)
- [Host it: docker-compose](../host-it/docker-compose.md)

## Next

- [Repos: multi-repo](repos-multi.md) — the bigger version.
- [First run](../get-it-running/first-run.md) — a `code` run against this config end to end.
