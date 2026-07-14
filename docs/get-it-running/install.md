# Install

Three ways to get Agent Smith on a machine. Pick the one that matches how you plan to run it.

- **CLI single-binary** — fastest path. Good for trying things out on your laptop, good for cron jobs and short-lived runs. Single download, no daemon.
- **Docker / docker-compose** — good for a small team setup or a long-lived server on one host. The server, a Redis, a one-shot database-migrate job and (optionally) the dashboard come up together with one `docker compose up`.
- **Kubernetes** — what you want once more than one person triggers runs, or once you need the server to survive a node restart. Standard Deployment + Service + ConfigMap + Secret, plus a tiny RBAC for the sandbox pods. The manifests ship in the repo under `deploy/k8s/`.

All three pull the same versioned images. Pin a version explicitly; the framework moves fast and you don't want surprise upgrades mid-run.

## CLI binary

The release page on GitHub publishes a single-file executable for macOS, Linux, and Windows. Download, chmod, run.

```bash
# Linux / macOS — adjust the version + arch for your machine
VERSION=0.108.0
curl -L -o agent-smith \
  https://github.com/holgerleichsenring/agent-smith/releases/download/v${VERSION}/agent-smith-linux-x64
chmod +x agent-smith
sudo mv agent-smith /usr/local/bin/

agent-smith --help
```

The CLI looks for `agentsmith.yml` in the current directory (then `./config/agentsmith.yml`, then your home directory); `--config /path/to/agentsmith.yml` overrides. Put one there before you run the first command. Minimal shape (the [first run](first-run.md) page walks you through it):

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/holgerleichsenring/agent-smith/main/config/agentsmith.schema.json

agents:
  default-openai:
    type: openai
    models:
      primary: { model: gpt-4.1 }

repos:
  todolist-api:
    type: github
    url: https://github.com/acme-org/todolist-api
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
    repos: [todolist-api]

secrets:
  openai_api_key: ${OPENAI_API_KEY}
  github_token:   ${GITHUB_TOKEN}
```

Set the secrets in your shell, then let the preflight tell you whether the wiring holds before you spend a single pipeline token:

```bash
export OPENAI_API_KEY=sk-...
export GITHUB_TOKEN=ghp_...

agent-smith doctor          # active preflight: config, LLM, tracker, repo, skills, sandbox, infra
agent-smith fix --ticket 54 --project todolist
```

`doctor` probes every configured dependency for real (it calls the LLM, authenticates against the tracker, spawns a throwaway sandbox) and prints one named check per known silent-failure class, each with a fix hint. Exit 0 means green; `--json` gives you a CI-gateable report. Run it after every config change — it's much cheaper than finding out twenty minutes into a run.

For CLI mode, the sandbox runs in-process by default (no Docker required, no isolation between repos). If you want the same multi-sandbox routing that Docker / k8s give you, set `SANDBOX_TYPE=docker` and have a Docker daemon running — Agent Smith will spin up real toolchain containers per repo.

## Docker / docker-compose

Use this when you want the server running as a service, not a one-shot CLI invocation. The compose file (in the repo under `deploy/`) brings up the server image, Redis for the in-flight job queue, a one-shot `database migrate` job that applies the schema before the server starts, and — behind the `dashboard` profile — the dashboard on port 3000.

Pull the images and check they're there:

```bash
docker pull holgerleichsenring/agent-smith-server:0.108.0
docker pull holgerleichsenring/agent-smith-cli:0.108.0
docker pull holgerleichsenring/agent-smith-sandbox-agent:0.108.0
docker pull redis:7-alpine
```

The full compose walkthrough is on the [docker-compose host page](../host-it/docker-compose.md). Drop your `agentsmith.yml` next to the compose file, set the secret env vars, and `docker compose up -d`.

## Kubernetes

For shared use and production. The server runs as a Deployment, gets its config from a ConfigMap and its secrets from a Secret, and is reachable on a Service so your tracker webhooks have something to POST to. The sandbox pods are created on demand and disposed at end-of-run, so you don't pre-provision them. The manifests are in the repo under `deploy/k8s/` — numbered, apply them in order. Details on the [kubernetes host page](../host-it/kubernetes.md); it runs on a stock cluster, no operators, no CRDs.

The server needs:
- A `ServiceAccount` with permission to create / delete pods in the same namespace (the sandbox pods).
- A `Service` of type `ClusterIP` (you'll front it with whatever ingress your cluster uses).
- A `Secret` with your AI provider key and your tracker token.
- A `ConfigMap` mounted at `/app/config/agentsmith.yml`.
- An init-container (the CLI image) running `agentsmith database migrate` — the server never migrates its own database on startup, by design.

## Pinning versions

Every release tag is published on Docker Hub and on the GitHub releases page. Server, CLI and sandbox-agent images ship from the same release, so there is exactly one pin in `agentsmith.yml`:

```yaml
deployment:
  registry: holgerleichsenring
  version: 0.108.0
```

That one `deployment:` block feeds both the orchestrator container and the sandbox-agent image. Bump it together with the image tag in your compose file / k8s manifest — that's the whole upgrade contract. Skills ship embedded in the release — every binary carries the exact catalog it was tested with, so there is nothing to pin. A `skills:` block in `agentsmith.yml` is an override for skills development or air-gap mirrors — see [Skills catalog](../how-it-works/skills-catalog.md).

## Next

Once Agent Smith is installed, [do your first run](first-run.md) — `agent-smith demo` proves the whole loop with nothing but an LLM key. Then [connect a tracker](../connect-your-stuff/tracker-azure-devops.md).
