# Trigger: CLI

The explicit, no-webhook way. Useful for testing the config, for dev iteration, and for one-off runs.

## Basic invocation

```bash
agent-smith fix --ticket 54 --project todolist
```

Reads `agentsmith.yml` from the current directory, looks up `todolist` under `projects:`, fetches ticket `54` from that project's tracker, runs the `fix-bug` pipeline. Output streams to stdout; exit code is zero on success.

The other pipelines have their own verbs:

```bash
agent-smith feature --ticket 62 --project todolist        # add-feature
agent-smith init --project todolist                       # init-project bootstrap
agent-smith security-scan --agent claude-default          # security scan
agent-smith api-scan --agent claude-parallel \
  --swagger https://api.todolist.dev/swagger.json \
  --target  https://api.todolist.dev                      # api-security-scan
agent-smith legal --source contract.pdf --project legal   # legal-analysis
agent-smith mad --ticket 71 --project todolist            # mad-discussion
agent-smith autonomous --project todolist                 # observe + write tickets
agent-smith compile-wiki --project todolist               # knowledge-base compile
agent-smith security-trend --project todolist             # scan trend analysis
```

`agent-smith --help` lists everything; every verb accepts `--config <path>` and `--verbose`, and the pipeline verbs accept `--dry-run` (print the pipeline, don't execute).

Two verbs are not pipelines but you'll use them a lot:

```bash
agent-smith doctor    # active preflight against every configured dependency
agent-smith demo      # run the whole loop on a bundled sample project (LLM key only)
```

## Scans without a project

For a one-off scan you don't need a project, a tracker, or a repo catalog entry at all. `--agent` names an agent from your config and builds an ephemeral project around the source path:

```bash
agent-smith api-scan --agent claude-parallel --source-path . \
  --swagger https://api.todolist.dev/swagger.json \
  --target  https://api.todolist.dev
```

`--project` still works on the scan verbs for back-compat, but `--agent` is the shape to use.

## Pass an explicit config path

```bash
agent-smith fix --ticket 54 --project todolist --config /etc/agentsmith.yml
```

Without `--config`, the CLI searches `./agentsmith.yml`, then `./config/agentsmith.yml`, then `~/agentsmith.yml`.

## Headless approval

The approval gate is on by default — the agent prints the plan and waits for `y`. To skip:

```bash
agent-smith fix --ticket 54 --project todolist --headless
```

For CI / cron / scripted runs, this is what you want. (Server-triggered runs — webhook, poll — are always headless.)

## Scope to one repo in a multi-repo project

```bash
agent-smith fix --ticket 54 --project azuredevops-todolist --repo todolist-api
```

Only touches `todolist-api`. Since p0331 you rarely need this by hand: the `ScopeRepos` step reads the ticket first and narrows the run to the affected repos on its own, before any sandbox is provisioned.

## Run against a local checkout

Skip the clone-from-remote step and use a local working directory instead:

```bash
agent-smith fix --ticket 54 --project todolist \
  --source-type local \
  --source-path ~/code/todolist-api \
  --repo todolist-api
```

The agent does its work in the local checkout. Useful for offline iteration and when you don't want to push to a remote. (`--source-url` and `--source-auth` exist for the symmetric case of overriding the remote.)

## What lands on disk

A run directory under `.agentsmith/runs/{run-id}/` with `plan.md`, `result.md`, and `decisions.md`. Same shape as a webhook-triggered run. See [First run](../get-it-running/first-run.md) for the walk-through.

## What the CLI doesn't do

- It doesn't listen for webhooks. The CLI is one-shot; webhooks need a long-running process. For that, go to [Host it: docker-compose](../host-it/docker-compose.md).
- It doesn't poll. Same reason.
- It doesn't do approval-via-comment. The interactive `y/N` prompt is on stdin only. The durable ask-and-resume dialogue is a server-mode feature — see [Expectations & durable dialogue](../how-it-works/expectations.md).

## Next

- [Webhooks](webhooks.md) — once the CLI invocation works, automate the trigger.
- [First run](../get-it-running/first-run.md) — the full walk-through.
- [Host it](../host-it/docker-compose.md) — move from CLI to a long-lived setup.
