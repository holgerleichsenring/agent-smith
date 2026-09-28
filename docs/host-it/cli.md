# Host it: CLI single-binary

The CLI binary is one process, one run, exit. Good for:

- Trying things out on your laptop without setting up Docker.
- Cron-driven runs (a Jenkins job that runs `agent-smith code` on a schedule).
- Air-gapped environments where you can't run a daemon.

For long-lived setups with webhooks, you want [docker-compose](docker-compose.md) or [kubernetes](kubernetes.md) instead.

## Where to put the config

The CLI looks for `agentsmith.yml`:

1. Path passed via `--config /path/to/agentsmith.yml`.
2. `./.agentsmith/agentsmith.yml` in the current working directory.
3. `./config/agentsmith.yml`.
4. `~/.agentsmith/agentsmith.yml` in your home directory.

For a one-machine setup, just keep `agentsmith.yml` in a project directory and `cd` there before invoking. For a shared CLI install (system-wide on a build agent), pass `--config /etc/agent-smith/agentsmith.yml` explicitly in the wrapper script / unit file.

## Secrets

Every `${VAR}` reference in `agentsmith.yml` resolves from the process environment at config-load time. Put your secrets in the shell:

```bash
export AZURE_OPENAI_API_KEY=...
export AZURE_DEVOPS_TOKEN=...
```

For systemd-managed runs:

```ini
[Service]
EnvironmentFile=/etc/agent-smith/secrets.env
ExecStart=/usr/local/bin/agent-smith code --ticket ${TICKET_ID} --project todolist --headless --config /etc/agent-smith/agentsmith.yml
```

For GitHub Actions / Azure Pipelines / GitLab CI, set them as masked CI secrets.

## Sandbox in CLI mode

The CLI always runs its sandbox in-process: file operations and commands happen on your local filesystem, in the CLI process, with no container around them and no isolation between repos. No Docker daemon is needed, and `SANDBOX_TYPE` has no effect here. The commands a run executes need their toolchain (`dotnet`, `node`, `git`, …) installed on the machine itself.

That's fine for single-repo work on a machine you trust. For per-repo toolchain containers, sandbox isolation and the package cache, run the [server](docker-compose.md), which picks Docker or Kubernetes sandboxes.

## Where a run leaves its record

The run record is written into each repository the run changed, under `.agentsmith/runs/<run>/` (`result.md`, and `plan.md` when the run had one), and committed on the run branch, so it travels with the pull request.

A CLI run also records its facts to a database, the same way a server run does. It uses the `persistence:` block when that location can be written. The default points at `/var/lib/agentsmith/agentsmith.db`, the container path, which a laptop usually can't create; then the run falls back to `~/.agentsmith/runs.db` and says so on the output.

## Exit codes

Zero on success (PR opened, ticket updated), non-zero on failure with the failing step + error message on the output. `agent-smith doctor` has the same contract — 0 all-green, 1 on any failed check — which makes it the natural gate before a scheduled run:

```bash
agent-smith doctor --json && agent-smith code --ticket "$TICKET" --project todolist --headless
```

Wrap in cron / CI accordingly.

## Updating

The CLI binary is one file. Replace it; that's the upgrade. There is no sandbox-agent image to match, because the CLI never starts a container. Skills come embedded in the binary and upgrade with it; a `skills:` block in `agentsmith.yml` is only needed to override that (path to a working tree, mirror URL, or an explicit `version:` pin).

## Next

- [docker-compose](docker-compose.md) — when you want webhooks and a long-running process.
- [Kubernetes](kubernetes.md) — when more than one person triggers runs.
- [First run](../get-it-running/first-run.md) — the actual walk-through.
