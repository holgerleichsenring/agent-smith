# Install

Three ways to get Agent Smith on a machine. Pick the one that matches how you plan to run it.

- **CLI single-binary** — fastest path. Good for trying things out on your laptop, good for cron jobs and short-lived runs. Single download, no daemon.
- **Docker / docker-compose** — good for a small team setup or a long-lived server on one host. The orchestrator, the sandbox-agent image, and a Redis come up together with one `docker compose up`.
- **Kubernetes** — what you want once more than one person triggers runs, or once you need the orchestrator to survive a node restart. Standard Deployment + Service + ConfigMap + Secret, plus a tiny RBAC for the sandbox pod.

All three pull the same versioned images. Pin a version explicitly; the framework moves fast and you don't want surprise upgrades mid-run.

## CLI binary

The release page on GitHub publishes a single-file executable for macOS, Linux, and Windows. Download, chmod, run.

```bash
# Linux / macOS — adjust the version + arch for your machine
VERSION=0.60.1
curl -L -o agent-smith \
  https://github.com/holgerleichsenring/agent-smith/releases/download/v${VERSION}/agent-smith-linux-x64
chmod +x agent-smith
sudo mv agent-smith /usr/local/bin/

agent-smith --version
```

The CLI looks for `agentsmith.yml` in the current directory by default. Put one there before you run the first command. Minimal shape (the [first run](first-run.md) page walks you through it):

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

Set the secrets in your shell:

```bash
export OPENAI_API_KEY=sk-...
export GITHUB_TOKEN=ghp_...
agent-smith fix "#54 in todolist"
```

For CLI mode, the sandbox runs in-process by default (no Docker required, no isolation between repos). If you want the same multi-sandbox routing that Docker / k8s give you, set `SANDBOX_TYPE=docker` and have a Docker daemon running — Agent Smith will spin up real toolchain containers per repo.

## Docker / docker-compose

Use this when you want the orchestrator running as a service, not a one-shot CLI invocation. The compose file pulls three things: the orchestrator image, the sandbox-agent image (an init-container that injects the agent binary into whatever toolchain image your repos use), and Redis for the in-flight job queue.

Pull the images and check they're there:

```bash
docker pull holgerleichsenring/agent-smith:0.60.1
docker pull holgerleichsenring/agent-smith-sandbox-agent:0.60.1
docker pull redis:7
```

The full compose file with the TodoList example is on the [docker-compose host page](../host-it/docker-compose.md). Drop your `agentsmith.yml` next to the compose file, set the secret env vars, and `docker compose up -d`.

## Kubernetes

For shared use and production. The orchestrator runs as a Deployment, gets its config from a ConfigMap and its secrets from a Secret, and is reachable on a Service so your tracker webhooks have something to POST to. The sandbox pods are created on demand by the orchestrator and disposed at end-of-run, so you don't pre-provision them.

Pull the same images, push them to your registry if you don't allow Docker Hub, and apply the manifests from the [kubernetes host page](../host-it/kubernetes.md). The example there uses the TodoList config and runs on a stock cluster — no operators, no CRDs.

The orchestrator needs:
- A `ServiceAccount` with permission to create / delete pods in the same namespace (the sandbox pods).
- A `Service` of type `ClusterIP` (you'll front it with whatever ingress your cluster uses).
- A `Secret` with your AI provider key and your tracker token.
- A `ConfigMap` mounted at `/etc/agent-smith/agentsmith.yml`.

## Pinning versions

Every release tag is published on Docker Hub and on the GitHub releases page. The version of the orchestrator and the version of the sandbox-agent image must match — there's a `sandbox` block in `agentsmith.yml` that names which sandbox-agent version to inject:

```yaml
sandbox:
  agent_registry: holgerleichsenring
  agent_version: 0.60.1

orchestrator:
  registry: holgerleichsenring
  version: 0.60.1
```

That's the upgrade contract: bump both numbers together. Skills are pinned separately under `skills.version` — see [Skills catalog](../how-it-works/skills-catalog.md).

## Next

Once Agent Smith is installed, [connect a tracker](../connect-your-stuff/tracker-azure-devops.md), then [do your first run](first-run.md).
