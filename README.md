# Agent Smith

> **From ticket to PR.**
> Every run shows its cost. Every change comes with the reasoning the agent followed.

[![Agent Smith](docs/agent-smith-logo-large-green.png)](https://docs.agent-smith.org)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com)
[![Docker](https://img.shields.io/badge/Docker-ready-blue.svg)](Dockerfile)

**[Docs](https://docs.agent-smith.org)** · **[Releases](https://github.com/holgerleichsenring/agent-smith/releases)** · **[Blog](https://codingsoul.org)**

---

Agent Smith is an open source AI coding agent. You drop a ticket into your tracker, and a pull request shows up on your repo, with the ticket already updated to point at it. That's the whole loop.

I built it because most AI coding tools stop at a suggestion in your editor and call it a day. I wanted to close the loop — actual PR, in the actual repo, the ticket actually moved to resolved. Plus a paper trail so six months later I can answer "why did we pick path A over B in this fix" without guessing.

![Lifecycle: ticket → orchestrator → sandboxes → pull requests → resolved](docs/assets/lifecycle.svg)

## What it does

You drop a ticket into your tracker. Agent Smith reads it, clones every repo in the project into its own sandbox (each with its own toolchain — a .NET repo gets `dotnet/sdk:8.0`, a Node repo gets `node:20`, a Python worker gets `python:3.12`), writes the code, runs the tests, opens one pull request per repo with the changes cross-linked, and writes the ticket back as resolved with every PR URL in the comment.

The reasoning the agent followed lands on disk in `.agentsmith/runs/{run-id}/` — a `plan.md`, a `result.md` with token usage and dollar cost, and a `decisions.md` for the non-obvious choices. Read it six months later when you've forgotten why.

## What it works with

| Trackers | AI providers | Hosting |
|---|---|---|
| Azure DevOps Boards | Anthropic Claude | CLI single-binary |
| Jira | OpenAI | Docker Compose |
| GitHub Issues | Azure OpenAI | Kubernetes |
| GitLab Issues | Google Gemini | |
| | Ollama (local) | |
| | OpenAI-compatible (Groq, vLLM, LM Studio, …) | |

The skills — the role definitions for what an architect / reviewer / security analyst does in a run — live in a [separate repo](https://github.com/holgerleichsenring/agent-smith-skills), pinned by tag in your `agentsmith.yml`. Skills update without a binary upgrade.

## Install

```bash
# Linux (x64)
curl -sL https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64 \
  -o /usr/local/bin/agent-smith && chmod +x /usr/local/bin/agent-smith

# macOS (Apple Silicon)
curl -sL https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-osx-arm64 \
  -o /usr/local/bin/agent-smith && chmod +x /usr/local/bin/agent-smith

# Docker
docker pull holgerleichsenring/agent-smith:latest
```

Every platform (Linux x64 / ARM64, macOS Intel / Apple Silicon, Windows) is on the [releases page](https://github.com/holgerleichsenring/agent-smith/releases). The [install guide](https://docs.agent-smith.org/get-it-running/install/) walks through CLI / Docker / Kubernetes setups.

## First run

Drop an `agentsmith.yml` in a working directory:

```yaml
agents:
  default-openai:
    type: openai
    models:
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
    repos: [todolist]

secrets:
  openai_api_key: ${OPENAI_API_KEY}
  github_token:   ${GITHUB_TOKEN}
```

Set the secrets, fix a ticket:

```bash
export OPENAI_API_KEY=sk-...
export GITHUB_TOKEN=ghp_...

agent-smith fix "#54 in todolist"
```

The [first-run page](https://docs.agent-smith.org/get-it-running/first-run/) shows the end-to-end output.

## More than fix-bug

`fix-bug` is the headline because it's the one most people show up for. Eight more presets ship in the box:

- `add-feature` — same flow plus generated tests and docs.
- `security-scan` — multi-role code security review.
- `api-security-scan` — Nuclei + Spectral + an AI panel against a live API.
- `legal-analysis` — contract review with five legal specialists.
- `mad-discussion` — multi-agent design discussion when you want to argue something out.
- `init-project` — bootstraps `.agentsmith/context.yaml` per repo in a project.
- `autonomous` — open-ended operator-driven loop.
- `skill-manager` — author / lint / validate skills.

Same orchestrator, different roles. You can define your own in `agentsmith.yml` too — see the [pipeline reference](https://docs.agent-smith.org/reference/pipelines/).

## Where the docs are

- **[Get it running](https://docs.agent-smith.org/get-it-running/install/)** — install + first run.
- **[Connect your stuff](https://docs.agent-smith.org/connect-your-stuff/tracker-azure-devops/)** — tracker + repos + AI provider, with a copy-pasteable YAML per system.
- **[Trigger it](https://docs.agent-smith.org/trigger-it/webhooks/)** — webhooks, polling, labels, CLI.
- **[Host it](https://docs.agent-smith.org/host-it/docker-compose/)** — CLI, Docker Compose, Kubernetes.
- **[How it works](https://docs.agent-smith.org/how-it-works/methodology/)** — the spec-first plan → review → verify → execute methodology.

## License

MIT. Copyright (c) 2026 Holger Leichsenring.

If you find Agent Smith useful, [say hi on the blog](https://codingsoul.org) or [drop an issue](https://github.com/holgerleichsenring/agent-smith/issues).
