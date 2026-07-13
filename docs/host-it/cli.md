# Host it: CLI single-binary

The CLI binary is one process, one run, exit. Good for:

- Trying things out on your laptop without setting up Docker.
- Cron-driven runs (a Jenkins job that runs `agent-smith fix` on a schedule).
- Air-gapped environments where you can't run a daemon.

For long-lived setups with webhooks, you want [docker-compose](docker-compose.md) or [kubernetes](kubernetes.md) instead.

## Where to put the config

The CLI looks for `agentsmith.yml`:

1. Path passed via `--config /path/to/agentsmith.yml`.
2. Path from `$AGENTSMITH_CONFIG`.
3. `./agentsmith.yml` in the current working directory.

Same precedence for `.agentsmith/skills/` (skills cache) and `.agentsmith/runs/` (run output directory) — relative to the config file.

For a one-machine setup, just keep `agentsmith.yml` in a project directory and `cd` there before invoking. For a shared CLI install (system-wide on a build agent), put it under `/etc/agent-smith/agentsmith.yml` and set `AGENTSMITH_CONFIG` globally.

## Secrets

Every `${VAR}` reference in `agentsmith.yml` resolves from the process environment at config-load time. Put your secrets in the shell:

```bash
export AZURE_OPENAI_API_KEY=...
export AZURE_DEVOPS_TOKEN=...
```

For systemd-managed runs:

```ini
[Service]
Environment=AGENTSMITH_CONFIG=/etc/agent-smith/agentsmith.yml
EnvironmentFile=/etc/agent-smith/secrets.env
ExecStart=/usr/local/bin/agent-smith fix "#${TICKET_ID} in todolist" --auto-approve
```

For GitHub Actions / Azure Pipelines / GitLab CI, set them as masked CI secrets.

## Sandbox in CLI mode

In CLI mode, the framework picks a sandbox backend automatically:

| Detection | Backend |
|---|---|
| `SANDBOX_TYPE=kubernetes` or pod env vars present | Kubernetes API (you probably don't want CLI mode if you have this) |
| `SANDBOX_TYPE=docker` or `/var/run/docker.sock` exists | Docker daemon — one container per repo per run |
| Otherwise | In-process sandbox — no isolation between repos, runs in the CLI process itself |

The in-process sandbox is the developer-machine convenience: no Docker daemon required, single-tenant, file ops happen on the local filesystem. It works fine for single-repo projects. For multi-repo with different toolchains you want the Docker backend, which means having a Docker daemon running.

To force a specific backend regardless of detection:

```bash
SANDBOX_TYPE=docker agent-smith fix "#54 in todolist"
```

## Output directory

`.agentsmith/runs/{run-id}/` next to the config file. Three files per run: `plan.md`, `result.md`, `decisions.md`. Plus a top-level `runs:` entry in `.agentsmith/context.yaml` for the wiki-compile pass.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Run succeeded. PR opened, ticket updated. |
| `1` | Run failed. Failing step + error message printed to stderr. |
| `2` | Config error. `agentsmith.yml` couldn't be loaded or is invalid. |
| `3` | Auth error. A secret was missing or invalid. |
| `4` | Ticket not found in the named tracker. |

Wrap in cron / CI accordingly.

## Updating

The CLI binary is one file. Replace it; that's the upgrade. The sandbox-agent image (for Docker mode) bumps separately — pull the new tag matching the new CLI version. Skills come embedded in the binary and upgrade with it; a `skills:` block in `agentsmith.yml` is only needed to override that (path to a working tree, mirror URL, or an explicit `version:` pin).

## Next

- [docker-compose](docker-compose.md) — when you want webhooks and a long-running process.
- [Kubernetes](kubernetes.md) — when more than one person triggers runs.
- [First run](../get-it-running/first-run.md) — the actual walk-through.
