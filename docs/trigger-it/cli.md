# Trigger: CLI

The explicit, no-webhook way. Useful for testing the config, for dev iteration, and for one-off runs.

## Basic invocation

```bash
agent-smith fix "#54 in todolist"
```

Reads `agentsmith.yml` from the current directory, looks up `todolist` under `projects:`, fetches ticket `#54` from that project's tracker, runs the `fix-bug` pipeline. Output streams to stdout; exit code is zero on success.

Other pipelines work the same way — first arg is the pipeline shortcut, rest is the natural-language "what":

```bash
agent-smith feature "#62 in todolist — add an export-to-csv button"
agent-smith init "todolist"
agent-smith security-scan "todolist"
agent-smith api-security-scan "todolist — scan https://api.todolist.dev"
agent-smith legal-analysis "contract.pdf in legal-todolist"
```

Pipeline shortcuts map 1:1 to the pipeline names — `fix` → `fix-bug`, `feature` → `add-feature`, `init` → `init-project`, etc.

## Pass an explicit config path

```bash
agent-smith --config /etc/agentsmith.yml fix "#54 in todolist"
```

Or set the env var once:

```bash
export AGENTSMITH_CONFIG=/etc/agentsmith.yml
agent-smith fix "#54 in todolist"
```

## Headless approval

The approval gate is on by default — the agent prints the plan and waits for `y`. To skip:

```bash
agent-smith fix "#54 in todolist" --auto-approve
```

For CI / cron / scripted runs, this is what you want.

## Scope to one repo in a multi-repo project

```bash
agent-smith fix "#54 in azuredevops-todolist" --repo todolist-api
```

Only touches `todolist-api`. The rest of the multi-repo project is ignored. Useful when you know the ticket only needs changes in one repo, or when you're testing the config for a single repo before turning on the full multi-repo flow.

## Run against a local checkout

Skip the clone-from-remote step and use a local working directory instead:

```bash
agent-smith fix "#54 in todolist" \
  --source-type local \
  --source-path ~/code/todolist-api \
  --repo todolist-api
```

The agent does its work in the local checkout. Useful for offline iteration and when you don't want to push to a remote.

## What lands on disk

A run directory under `.agentsmith/runs/{run-id}/` with `plan.md`, `result.md`, and `decisions.md`. Same shape as a webhook-triggered run. See [First run](../get-it-running/first-run.md) for the walk-through.

## Skip-trigger mode

The CLI doesn't write the framework lifecycle labels back to the tracker by default when run interactively. To behave exactly like a webhook-triggered run (write lifecycle labels, close ticket on success):

```bash
agent-smith fix "#54 in todolist" --write-lifecycle-labels
```

Don't set this when iterating on the config — you'll churn the ticket history with status changes you didn't mean.

## What the CLI doesn't do

- It doesn't listen for webhooks. The CLI is one-shot; webhooks need a long-running process. For that, go to [Host it: docker-compose](../host-it/docker-compose.md).
- It doesn't poll. Same reason.
- It doesn't do approval-via-comment. The interactive `y/N` prompt is on stdin only.

## Next

- [Webhooks](webhooks.md) — once the CLI invocation works, automate the trigger.
- [First run](../get-it-running/first-run.md) — the full walk-through.
- [Host it](../host-it/docker-compose.md) — move from CLI to a long-lived setup.
