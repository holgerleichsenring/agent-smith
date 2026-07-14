# Repos: mono-repo

One product, one repo. The smallest viable Agent Smith setup. Use this page when your `TodoList` (or whatever it really is) lives in a single git repo.

## The whole config

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
    organization: acme-org
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

That's it. The `repos:` list on `projects.todolist` has one entry, the project is a mono-repo, and Agent Smith spawns one sandbox per run. Skills need no configuration — they ship embedded in the release; a `skills:` block is only an override for skills development or air-gap mirrors (see [Skills catalog](../how-it-works/skills-catalog.md)).

The explicit `repos:` catalog entry shown here is the right shape for exactly this case — a single, known repo. It also stays available as the escape hatch for a repo that isn't discoverable through a provider API. The moment you have several repos under one org, prefer a `connections:` entry and let Agent Smith discover them — see [Repos: multi-repo](repos-multi.md).

## What happens at run-time

For a ticket targeted at the `todolist` project:

1. Agent Smith spawns one sandbox with the toolchain image matching the repo's primary language (auto-detected from `.agentsmith/context.yaml` in the repo, or fall back to a generic image).
2. The repo gets cloned into `/work` inside the sandbox.
3. A branch named `agentsmith/ticket-{N}` is cut from the default branch.
4. The agent reads, plans, writes, tests — all inside that one sandbox.
5. The commit is pushed; one pull request is opened.
6. The ticket is updated with the PR URL.

The internal mechanics are the same as for multi-repo runs — there's just one sandbox in the dict instead of N.

## What changes if you grow a second repo

You add the new repo to the top-level `repos:` catalog and reference it from `projects.todolist.repos` — or, better, declare a `connections:` entry once (host, org, auth) and reference repos under it by glob, so new repos are discovered instead of hand-listed. The lifecycle code is identical either way; multi-repo just means the list has more than one entry. See [Repos: multi-repo](repos-multi.md) for the worked example.

The interesting bit: every repo in the project needs its own `.agentsmith/context.yaml` (and ideally a `.agentsmith/coding-principles.md`) so Agent Smith knows the toolchain and conventions for each. The `init-project` pipeline bootstraps that for you per repo. Run it once per repo before the first `fix-bug` against the project.

## What about pipelines, triggers, hosting?

Same as multi-repo. Trigger config goes in `projects.X.{tracker}_trigger` regardless of whether the project has one repo or fifteen. See:

- [Trigger it: webhooks](../trigger-it/webhooks.md)
- [Trigger it: labels](../trigger-it/labels.md)
- [Host it: docker-compose](../host-it/docker-compose.md)

## Next

- [Repos: multi-repo](repos-multi.md) — the bigger version.
- [First run](../get-it-running/first-run.md) — a `fix-bug` against this config end to end.
