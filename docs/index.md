<img src="agent-smith-logo-large-green.png" alt="Agent Smith" style="max-width: 600px;">

# From ticket to PR

Agent Smith is an open source AI coding agent. You drop a ticket into your tracker, and a PR shows up on your repo, with the ticket already updated to point at it. Every run writes down what it cost in tokens and dollars, and every change it made comes with the reasoning the agent followed. That's the whole loop.

This page is the orientation. There's a fast-path link list at the bottom — if you're here to ship today, skip the rest and jump straight in.

## What it does, in one paragraph

You drop a ticket into your tracker. Agent Smith reads it, clones every repo in the project into its own sandbox (each with its own toolchain image — a .NET repo gets `dotnet/sdk:8.0`, a Node repo gets `node:20`), generates a plan, lets you approve it (or runs headless if you've told it to), writes the code, runs the tests, opens one pull request per repo with the changes cross-linked, and writes the ticket back as resolved with every PR URL in the comment.

![Lifecycle: ticket → orchestrator → sandboxes → pull requests → resolved](assets/lifecycle.svg)

## What lands on disk after a run

Every run gets a directory under `.agentsmith/runs/`. The directory name is the run id — a UTC timestamp plus a 4-hex collision suffix plus a slug.

```
.agentsmith/runs/2026-05-22T14-03-11-9f2a-fix-login-bug/
├── plan.md       — the plan the agent followed, role-by-role
├── result.md     — what got done, cost in tokens and USD
└── decisions.md  — non-obvious choices made during the run
```

`plan.md` and `decisions.md` are the why-record. Six months later, when you've forgotten why the agent picked path A over path B, the answer is in there. That was the reason I built it this way — I got tired of code I couldn't reverse-engineer.

## What's supported

**Trackers** — these are the ticket sources Agent Smith reads from and writes back to.

| Tracker | Trigger modes | Connect page |
|---|---|---|
| Azure DevOps Boards | webhook · polling · label | [Azure DevOps](connect-your-stuff/tracker-azure-devops.md) |
| Jira | webhook · polling · label | [Jira](connect-your-stuff/tracker-jira.md) |
| GitHub Issues | webhook · label | [GitHub Issues](connect-your-stuff/tracker-github-issues.md) |
| GitLab Issues | webhook · polling · label | [GitLab Issues](connect-your-stuff/tracker-gitlab-issues.md) |

**AI providers** — Agent Smith calls these directly from your infrastructure. You pick the model per role (a cheap one for scout passes, the good one for the actual code).

| Provider | Notes |
|---|---|
| Anthropic Claude | First-class. Prompt caching on by default. |
| OpenAI | First-class. Reasoning models supported. |
| Azure OpenAI | Same as OpenAI plus per-deployment routing. |
| Google Gemini | First-class. |
| Ollama | Local models. No API key, no internet. |
| OpenAI-compatible | Groq, LM Studio, vLLM, your own endpoint. |

See [Connect your AI provider](connect-your-stuff/ai-providers.md) for the config blocks.

**Skills** — the role definitions (architect, backend dev, security analyst, contract reviewer, …) live in a separate repo, `github.com/holgerleichsenring/agent-smith-skills`, versioned with release tags. You pin a tag in your `agentsmith.yml` and you're done. The skills repo gets updates without touching your Agent Smith binary, and the binary gets updates without touching your skills. See [Skills catalog](how-it-works/skills-catalog.md) for the pin strategy.

## Get running today

The pages below are the fast-path. They assume you have Docker (or a Kubernetes cluster you can deploy to) and an API key for an AI provider. If you've got that, you're ten minutes away from your first run.

1. [Install](get-it-running/install.md) — CLI binary, Docker image, or k8s.
2. [Connect your tracker](connect-your-stuff/tracker-azure-devops.md) — pick the page for your tracker.
3. [Connect your repos](connect-your-stuff/repos-mono.md) — single repo, or [multi-repo](connect-your-stuff/repos-multi.md) if your project spans more than one.
4. [Connect your AI provider](connect-your-stuff/ai-providers.md) — the config block for the provider you have.
5. [First run](get-it-running/first-run.md) — `agent-smith fix "#54 in todolist"` end to end.

Then [pick a trigger mode](trigger-it/webhooks.md) (webhook is what you want for production) and [pick a host setup](host-it/docker-compose.md) (docker-compose is the easiest, k8s is what you want for shared use).

## How it works, when you have time

- [Methodology](how-it-works/methodology.md) — the spec-first plan→review→verify→execute flow and why.
- [Lifecycle](how-it-works/lifecycle.md) — what happens between ticket-in and ticket-back.
- [Multi-repo pipelines](how-it-works/multi-repo.md) — one ticket, N sandboxes, N pull requests.
- [Skills catalog](how-it-works/skills-catalog.md) — where skills live, how versioning works.

Everything else lives in Reference. That's where the per-pipeline pages, the full [agentsmith.yml schema](reference/configuration/agentsmith-yml-schema.md), the [architecture deep-dives](reference/architecture/index.md), the [pipeline-specific pages](reference/pipelines/index.md) and the historical run-logs sit.

## License

MIT. Copyright (c) 2026 Holger Leichsenring. Source at [`github.com/holgerleichsenring/agent-smith`](https://github.com/holgerleichsenring/agent-smith).
