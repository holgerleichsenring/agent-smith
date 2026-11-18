<div align="center">

<a href="https://codeably.ai"><img src="docs/assets/readme-banner.svg" alt="codeably.ai: a ticket becomes a spec, the spec is reviewed, built in sandboxes, passes the gate and arrives as a pull request" width="100%"></a>

### Make work programmable. Open source, on your infrastructure.

codeably was called Agent Smith until November 2026. The CLI, the config file `agentsmith.yml`, the images and this repository still carry that name; their rename follows.

[![License: MIT](https://img.shields.io/badge/license-MIT-dfb317.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512bd4.svg)](https://dotnet.microsoft.com)
[![Release](https://img.shields.io/github/v/release/holgerleichsenring/agent-smith?color=2ea44f)](https://github.com/holgerleichsenring/agent-smith/releases)
[![Tests](https://img.shields.io/badge/tests-10%2C000%2B-2ea44f.svg)](tests/)
[![Specs](https://img.shields.io/badge/specs-1%2C300%2B%20done-0969da.svg)](.agentsmith/specs/done/)

**[Website](https://codeably.ai)** · **[Docs](https://docs.codeably.ai)** · **[Install](#install)** · **[Releases](https://github.com/holgerleichsenring/agent-smith/releases)** · **[How it was built](#built-by-the-method-it-teaches)**

</div>

| Ticket to PR | Proved, not trusted | Your stack |
|---|---|---|
| Reads the ticket, writes a spec, builds it in sandboxes and opens one pull request per repo, cross-linked. | A separate reader checks every done criterion and cites what on the branch proves it. | Azure DevOps, Jira, GitHub, GitLab. Claude, OpenAI, Gemini, Ollama. CLI, Docker Compose or Kubernetes. |

---

codeably is an open source agentic software factory, a project led by its creator, Holger Leichsenring. You drop a ticket into your tracker; it becomes a spec, the spec gets built in sandboxes and verified criterion by criterion, and a pull request shows up on your repo, with the ticket already updated to point at it. That's the whole loop.

We built it because most AI coding tools stop at a suggestion in your editor and call it a day. We wanted to close the loop — actual PR, in the actual repo, the ticket actually moved to resolved. Plus a paper trail so that long after the fact we can answer "why did we pick path A over B in this fix" without guessing.

![Lifecycle: ticket → orchestrator → sandboxes → pull requests → resolved](docs/assets/lifecycle.svg)

## What it does

You drop a ticket into your tracker. codeably reads it and derives a specification: the specs the work needs and, per spec, what has to be true when it's done. It clones every repo in the project into its own sandbox (each with its own toolchain — a .NET repo gets `dotnet/sdk:8.0`, a Node repo gets `node:20`, a Python worker gets `python:3.12`), works the specs in order, runs the verification each repository declares, opens one pull request per repo with the changes cross-linked, and writes the ticket back with every PR URL in the comment.

Tickets in Azure Boards and code in Azure Repos? That works end to end, no GitHub mirror needed — [the Azure DevOps page](https://codeably.ai/azure-devops/) says why that matters.

The reasoning lands on disk: the specification under `.agentsmith/specs/`, every choice with the alternative it beat under `.agentsmith/decisions/`, and per run a `result.md` with the delivery account, token usage and dollar cost. Read it later, when you've forgotten why.

## What decides that a run delivered

Green tests aren't the verdict. After a spec's verification passes, a separate reader goes through its done-list one criterion at a time — met, unmet or not applicable — and has to cite what on the branch proves each one. That account is the only thing the run's verdict listens to. A run that comes back short still opens its pull request, as a draft, with a section naming what was not delivered, and you can overrule a single verdict with a reason that stays on the record. The [delivery account page](https://docs.codeably.ai/how-it-works/delivery-account/) has the details, and the [lifecycle page](https://docs.codeably.ai/how-it-works/lifecycle/) walks through a run step by step.

If you'd rather think the change through first, the dashboard's [Work it out](https://docs.codeably.ai/how-it-works/work-it-out/) page is a conversation with an agent that reads your code while it answers. What you approve there is filed as tickets with their reviewed specs, and each run builds exactly those specs.

## The dashboard

Work starts from one question, "What should we work on?". The first message opens a conversation that reads your code, drafts tickets with reviewed specs and files them when you say so.

![Work it out: a conversation that drafted three tickets, each with its reviewed specs, and the File button](docs/assets/screenshots/work-it-out.png)

A ticket has one page: what happened, the specs as they stand on its branch, the acceptance verdicts, every run and what it cost. When a run hits a decision it shouldn't make alone, it asks there, and no tokens burn while it waits.

![A ticket page: what happened, with tracker comments and two runs](docs/assets/screenshots/ticket-page.png)

A run reads as one log. Open a step and you see every LLM call in order, what it was fed, what it decided and what it cost.

![A run step opened in place, listing its LLM calls](docs/assets/screenshots/run-step-llm-calls.png)

The walkthrough with every stage pictured is at [docs.codeably.ai/how-it-works/end-to-end](https://docs.codeably.ai/how-it-works/end-to-end/).

## What it works with

| Trackers | AI providers | Hosting |
|---|---|---|
| Azure DevOps Boards | Anthropic Claude | CLI single-binary |
| Jira | OpenAI | Docker Compose |
| GitHub Issues | Azure OpenAI | Kubernetes |
| GitLab Issues | Google Gemini | |
| | Ollama (local) | |
| | GitHub Copilot (on your seat) | |

The skills — the masters that drive each pipeline and the rules they follow — are developed in a [separate repo](https://github.com/holgerleichsenring/agent-smith-skills), and every release ships with its catalog embedded. The binary you download carries the exact skills it was tested with; there is nothing to pin and nothing to fetch on first run. A `skills:` block in the config is only for overriding that (skills development, air-gap mirrors).

## Built by the method it teaches

codeably bootstraps an `.agentsmith/` directory into your repo: context, specs, a decision log, a memory of what it learned. That is the product. It is also how this repository got written, by Holger Leichsenring and one language model. The numbers below are the receipts.

|  |  |
|---|---|
| **1,332** | completed specs, each specified before a line of code existed |
| **6,806** | recorded decisions, each naming the alternative it beat |
| **743,663** | lines of C# |
| **10,944** | automated tests, run on every spec commit |
| **1 in 6** | specs stopped by the commit gate at least once (111 of 660 since the gate's ledger started on 2026-08-24) |

Two things did the actual steering. Ten coding principles turned into [architecture tests](tests/AgentSmith.Tests/Architecture/) that fail a build, and every one of them has a concrete thing that went wrong behind it. Then a [blocking commit hook](.claude/hooks/spec-gate.sh) that lets a spec commit through once the dashboard's build and tests, the backend build, all 10,944 tests, four CLI dry runs and every harness preset come back green. CI would have told us about a break afterwards. The hook stops the commit from existing, and the model has no way to wave itself through.

The `principles.md` we built this project under is the same file codeably injects into its own agents at runtime. The methodology and the product turned out to be the same thing.

**[The full account, including what didn't work](https://docs.codeably.ai/how-it-works/built-by-the-method/)**: the skill catalog going from 95 down to 12, the plan generator we retired, the metric that quietly read zero without anyone noticing, and how every figure above was counted.

## Install

```bash
# Linux (x64)
curl -sL https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64 \
  -o /usr/local/bin/agent-smith && chmod +x /usr/local/bin/agent-smith

# macOS (Apple Silicon)
curl -sL https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-osx-arm64 \
  -o /usr/local/bin/agent-smith && chmod +x /usr/local/bin/agent-smith

# Docker (long-running server)
docker pull holgerleichsenring/agent-smith-server:latest
```

Every platform (Linux x64 / ARM64, macOS Intel / Apple Silicon, Windows) is on the [releases page](https://github.com/holgerleichsenring/agent-smith/releases). The [install guide](https://docs.codeably.ai/get-it-running/install/) walks through CLI / Docker / Kubernetes setups.

## First run

Prove the loop before you connect anything. The only credential the demo needs is an LLM key:

```bash
export OPENAI_API_KEY=sk-...
agent-smith demo
```

That materializes a small sample project with a seeded bug, runs the real `code` pipeline against it, and leaves you a local commit plus the diff. No tracker, no Docker, no Redis — if this works, the loop works.

For your real systems, put a `config/agentsmith.yml` in a working directory (or pass `--config`):

```yaml
agents:
  default-openai:
    type: openai
    model: gpt-4.1        # every role on one model; see AI providers for a model per role

repos:
  todolist:
    type: github
    url: https://github.com/acme-org/todolist
    auth: github_token

trackers:
  acme-issues:
    type: github
    url: https://github.com/acme-org/todolist    # the repo whose issues are the tickets
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

Set the secrets, let the doctor check the wiring, fix a ticket:

```bash
export OPENAI_API_KEY=sk-...
export GITHUB_TOKEN=ghp_...

agent-smith doctor
agent-smith code --ticket 54 --project todolist
```

`doctor` actually probes everything — it calls the LLM, authenticates against the tracker, spawns a throwaway sandbox — and names what's broken with a fix hint, before a run spends tokens on it. The [first-run page](https://docs.codeably.ai/get-it-running/first-run/) shows the end-to-end output.

## More than the code pipeline

`code` is the headline because shipping a change is the one most people show up for — one pipeline for bug, feature and spec tickets alike, because what differs between them is the specification it derives, not the steps it runs. The rest of the box:

- `pr-review` — reviews a PR diff and posts line-anchored findings as comments; re-review on push replaces them.
- `security-scan` — a security master reviews the code, reconciles it with the scanners, and every finding it delivers has to survive a refuter.
- `api-security-scan` — Nuclei, Spectral and ZAP against a live API, judged by the same kind of master.
- `legal-analysis` — contract review.
- `mad-discussion` — multi-perspective design discussion when you want to argue something out.
- `init-project` — bootstraps `.agentsmith/contexts/<name>/` per component in each repo of a project.
- `spec-dialog` — the design conversation that ends in a filed ticket with an approved specification.

Same orchestrator, different masters. The [pipeline reference](https://docs.codeably.ai/reference/pipelines/) has each one.

And two things that took the longest to get right, so we'll name them here: a run doesn't take its own word for being done — every acceptance criterion gets a verdict with evidence from a reader that never saw the work being done. And when a run has a question, it checkpoints and waits — days if needed — without holding a pod. A ticket too thin to work from gets asked, not guessed at. There's also a conversational side ([Work it out](https://docs.codeably.ai/how-it-works/work-it-out/) in the dashboard, or [Slack and Teams](https://docs.codeably.ai/how-it-works/spec-dialogue/)): talk the change through, approve the specification, and it files the ticket.

## Where the docs are

- **[Get it running](https://docs.codeably.ai/get-it-running/install/)** — install + first run (`demo`, `doctor`).
- **[Connect your stuff](https://docs.codeably.ai/connect-your-stuff/tracker-azure-devops/)** — tracker + repos + AI provider, with a copy-pasteable YAML per system.
- **[Trigger it](https://docs.codeably.ai/trigger-it/webhooks/)** — webhooks, polling, labels, CLI.
- **[Host it](https://docs.codeably.ai/host-it/docker-compose/)** — CLI, Docker Compose, Kubernetes with honest capacity quotas.
- **[How it works](https://docs.codeably.ai/how-it-works/methodology/)** — the spec-first methodology, the delivery account, Work it out.
- **[Dashboard](https://docs.codeably.ai/reference/operations/dashboard/)** — watch runs live: every step, every LLM call with its cost and cached share, a cancel button that means it.

## Who is behind it

codeably is led by its creator, Holger Leichsenring, and shaped by the teams that use it in production. Contributions are welcome, see [CONTRIBUTING.md](CONTRIBUTING.md). Companies that need a contract, an SLA or onboarding work with [45level](https://45level.com), the project's professional-services partner.

How the project is run and who decides what is in [GOVERNANCE.md](GOVERNANCE.md).

## License

MIT. Copyright (c) 2026 Holger Leichsenring.

If you find codeably useful, [say hi on the blog](https://codingsoul.org) or [drop an issue](https://github.com/holgerleichsenring/agent-smith/issues).
