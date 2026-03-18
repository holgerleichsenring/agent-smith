# Agent Smith

**Your AI. Your infrastructure. Your rules.**

Self-hosted AI orchestration · code · legal · security · workflows

[![Agent smith](/docs/agent-smith-logo-large-green.png)](logo)

Agent Smith is an open-source framework for building and running AI-powered workflows. You define a pipeline as a chain of steps. Each step has a handler. Steps can spawn sub-agents, run multi-role discussions, call tools, or just move files around. The framework handles orchestration, tool calling, cost tracking, and delivery.

Out of the box it ships with pipelines for fixing bugs, adding features, analyzing legal contracts, scanning code for security vulnerabilities, and running multi-agent design discussions. Those are presets. You can build your own.

Everything runs on your infrastructure. Your API keys. Your data. Cloud models or local ones on your own GPU. No SaaS platform in between.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com)
[![Docker](https://img.shields.io/badge/Docker-ready-blue.svg)](Dockerfile)

---

## What It Does

```
You:           "fix #54 in todo-list"
               ↓
 [1/13] FetchTicket          → Reads ticket from Azure DevOps / GitHub / Jira / GitLab
 [2/13] CheckoutSource       → Clones repo, creates branch fix/54
 [3/13] BootstrapProject     → Detects language, framework, project type
 [4/13] LoadCodeMap          → Generates navigable code map
 [5/13] LoadDomainRules      → Loads your coding standards & domain rules
 [6/13] LoadContext          → Loads project context (.agentsmith/context.yaml)
 [7/13] AnalyzeCode          → Scout agent maps the codebase, identifies relevant files
 [8/13] GeneratePlan         → AI generates a step-by-step implementation plan
 [9/13] Approval             → Shows plan, waits for your OK (or runs headless)
[10/13] AgenticExecute       → AI agent writes the code, iterating with tools
[11/13] Test                 → Runs your test suite
[12/13] WriteRunResult       → Writes run result with token usage & cost data
[13/13] CommitAndPR          → Commits, pushes, opens PR, closes ticket
               ↓
Agent Smith:   "Pull request created: https://github.com/.../pull/42"
```

That's the standard bug fix pipeline. There are others.

---

## Pipelines

Agent Smith is not a one-trick pony. It ships with seven pipeline presets and you can define your own.

### Code Pipelines

**fix-bug** is the workhorse. Thirteen steps from ticket to PR. Reads the issue, clones the repo, understands the codebase, writes a plan, executes it, runs your tests, and opens a pull request. If the tests fail, it tries again.

**add-feature** does the same but also generates unit tests and documentation after the implementation is done. Fourteen steps.

**fix-no-test** is for those moments when you just need the code changed and don't want to wait for the test suite. Same as fix-bug but skips the test step. Use responsibly.

**init-project** bootstraps a new repo. Three steps: clone, detect everything about the project (language, framework, architecture, coding conventions), and commit the generated `.agentsmith/` directory.

### Security Scanning

**security-scan** is the newest pipeline. Point it at a repo or a pull request and it runs a team of security specialists over the code: a Vulnerability Analyst who checks for OWASP Top 10, an Auth Reviewer who knows JWT and OAuth inside out, an Injection Checker who traces every user input to every database query, a Secrets Detector who finds your hardcoded API keys, and a False Positive Filter who throws out everything that smells like noise. Confidence below 8 out of 10? Gone.

Output comes in three flavors. SARIF for your GitHub Security tab or GitLab security widget. Markdown for a PR comment that your team can read. Console for local runs where you just want to see what's wrong.

```bash
agent-smith security-scan --repo ./my-api --output sarif
agent-smith security-scan --repo . --pr 42 --output markdown
```

### Legal Analysis

**legal-analysis** has nothing to do with code. Drop a contract (PDF, DOCX, whatever) into the inbox folder and Agent Smith converts it to Markdown, detects the contract type, and sends it through a panel of five legal specialists: a Contract Analyst who reads every clause, a Compliance Checker who knows DSGVO and AGB-Recht by heart, a Risk Assessor who rates every clause from green to red, a Liability Analyst who deep-dives into the scary parts, and a Clause Negotiator who writes alternative formulations for the problematic bits. All output is in German legal language. This is not legal advice. It's a pre-review aid that saves your lawyer eight hours of reading.

The inbox folder is watched by a polling service. New documents get picked up automatically, analyzed, and the result lands in the outbox. Source documents are archived. No manual intervention needed.

### Discussion Pipelines

**mad-discussion** stands for Multi-Agent Discussion. Instead of writing code immediately, Agent Smith assembles a panel of specialists (Architect, Tester, DevOps, DBA, Security Reviewer) who debate the approach in rounds. They raise objections, make suggestions, and argue until they converge on a plan. Think of it as a design review that happens in thirty seconds instead of three meetings.
---

## AI Providers

Every provider is swappable. No vendor lock-in. Change one line in the config and you're on a different model.

**Claude** from Anthropic is the default. Sonnet for the heavy lifting, Haiku for the quick stuff like scouting and summarization. Prompt caching keeps costs down on repeated runs.

**GPT-4** from OpenAI works the same way. Same tool calling, same agentic loop, same pipeline.

**Gemini** from Google. Same deal.

**Ollama** is the new kid. Run models locally on your own hardware. Qwen2.5-Coder, DeepSeek-R1, Mistral Small, Llama 3.3. Zero API costs. Agent Smith auto-detects whether the model supports tool calling. If it does, native tools. If it doesn't, structured text fallback. Transparent to the rest of the pipeline.

The model registry lets you mix providers per task. Use Claude for planning (where quality matters), Ollama for execution (where cost matters), and Haiku for scouting (where speed matters). All in one config file.

```yaml
agent:
  type: Ollama
  model: qwen2.5-coder:32b
  endpoint: http://ollama-server:11434
```

Or mix cloud and local:

```yaml
models:
  scout:
    model: mistral-small:3.1
    provider_type: Ollama
    endpoint: http://ollama:11434
  planning:
    model: claude-sonnet-4-20250514
  execution:
    model: qwen2.5-coder:32b
    provider_type: Ollama
    endpoint: http://ollama:11434
```

---

## Ticket Systems and Source Control

Agent Smith talks to everything.

**GitHub** for issues and pull requests via Octokit. **Azure DevOps** for work items and repos via the DevOps SDK. **Jira** for tickets via REST v3. **GitLab** for issues and merge requests via their REST API. **Local** for repos that are already on disk.

Every source provider now also posts PR comments. When the security scanner finds something, it posts a summary table directly on the pull request. Same for Azure DevOps and GitLab merge requests.

---

## The Agentic Loop

The AI doesn't generate code once and call it done. It runs in a loop with real tools: read files, write files, list directories, execute shell commands. If the tests fail, it reads the error, fixes the code, and runs them again. If it realizes the plan was wrong, it logs a decision explaining why it deviated.

Speaking of decisions. Every architectural choice, tooling decision, and trade-off the agent makes gets written to `.agentsmith/decisions.md` in the target repository. Not what it did, but why. "DuckDB over direct OneLake access: RBAC setup via abfss:// too complex for first run." That kind of thing. When the agent's code breaks six months from now, you'll know what it was thinking.

Decisions also show up in the run results. Every `result.md` has a Decisions section grouped by category.

---

## Multi-Skill Architecture

For complex tickets, Agent Smith doesn't just throw one AI at the problem. A triage step looks at the ticket and the codebase and selects the right specialists. An Architect might debate with a DBA about the schema change while a Security Reviewer flags the missing input validation.

Roles are defined in YAML files under `config/skills/`. Each role has triggers (what activates it), rules (how it behaves), and convergence criteria (when it's satisfied). You can add your own roles. You can disable roles per project. You can add project-specific rules that override the defaults.

The discussion runs in rounds. Each round, every active role states its position: agree, object with reason, or suggest an alternative. When all roles agree, the discussion converges and the consolidated plan goes to execution. There's a hard limit to prevent endless debates.

---

## Deployment

### Docker

```bash
docker run --rm \
  -e ANTHROPIC_API_KEY=sk-ant-... \
  -e GITHUB_TOKEN=ghp_... \
  -v ~/.ssh:/home/agentsmith/.ssh:ro \
  -v ./config:/app/config \
  holgerleichsenring/agent-smith \
  run --headless "fix #42 in my-project"
```

### Docker Compose

```bash
git clone https://github.com/holgerleichsenring/agent-smith.git
cd agent-smith
cp .env.example .env   # add your API keys
docker compose up -d
```

The compose file runs four services: the agent runner (one-shot), a webhook server, Redis, and the Dispatcher. Want local models? Add the Ollama profile:

```bash
docker compose --profile local-models up -d
docker exec ollama ollama pull qwen2.5-coder:32b
```

### Kubernetes

K8s manifests live in `k8s/`. Kustomize overlays for dev and prod. The Dispatcher spawns ephemeral Jobs for each ticket. Each Job clones the repo, runs the pipeline, and terminates. Progress streams back to Slack via Redis.

### CI/CD

Every push to `main` builds both Docker images and publishes them to Docker Hub. Multi-arch (amd64 and arm64). Semver tags on git tags. PRs get a build-only check.

---

## CLI

```
agent-smith run "fix #42 in my-project"                    # process a ticket
agent-smith run "fix #42 in my-project" --dry-run          # show pipeline only
agent-smith run "fix #42 in my-project" --headless          # no approval prompt
agent-smith security-scan --repo . --output sarif           # scan for vulnerabilities
agent-smith security-scan --repo . --pr 42 --output markdown # scan a PR
agent-smith server --port 8081                              # webhook listener mode
```

---

## Chat Gateway

Talk to Agent Smith from Slack or Teams.

```
fix #65 in todo-list
→ Starting Agent Smith for ticket #65 in todo-list...
→ [1/13] FetchTicketCommand...
→ [10/13] AgenticExecuteCommand...
→ Done! Pipeline completed successfully · View Pull Request
```

Each request runs in its own ephemeral container. Progress, questions, and results stream back to your channel in real time.

---

## Cost Tracking

Every run writes a `result.md` with YAML frontmatter containing token usage, cost per phase, and duration. Machine-parseable with `yq` and human-readable at the same time.

```yaml
---
ticket: "#57 — GET /todos returns 500 when database is empty"
date: 2026-02-24
result: success
duration_seconds: 50
cost:
  total_usd: 0.0682
  phases:
    scout:
      model: claude-haiku-4-5-20251001
      turns: 3
      usd: 0.0062
    primary:
      model: claude-sonnet-4-20250514
      turns: 7
      usd: 0.0620
---
```

Ollama tasks show `$0.00`. Because they're free. That's the point.

---

## Architecture

```
┌──────────────────────────────────────────────────────┐
│                  AgentSmith.Host                      │
│   CLI (System.CommandLine subcommands)                │
└──────────────┬───────────────────────────────────────┘
               │
┌──────────────▼───────────────────────────────────────┐
│               AgentSmith.Application                  │
│   ProcessTicketUseCase → PipelineExecutor             │
│   22 Command Handlers (one per pipeline step)         │
└──────────────┬───────────────────────────────────────┘
               │
┌──────────────▼───────────────────────────────────────┐
│        AgentSmith.Infrastructure.Core                 │
│   Config, Detection, CodeMap, ProviderRegistry        │
├──────────────────────────────────────────────────────┤
│           AgentSmith.Infrastructure                   │
│   AI:     Claude / OpenAI / Gemini / Ollama           │
│   Tickets: AzureDevOps / GitHub / Jira / GitLab      │
│   Source:  AzureRepos / GitHub / GitLab / Local       │
│   Output:  SARIF / Markdown / Console                 │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│             AgentSmith.Dispatcher                     │
│   Slack / Teams Adapters                             │
│   Redis Streams Message Bus                          │
│   K8s / Docker Job Spawner                           │
└──────────────────────────────────────────────────────┘
```

Clean Architecture. Every layer depends only inward. Every provider is behind an interface. 492 tests make sure it stays that way.

---

## Project Structure

```
agent-smith/
├── src/
│   ├── AgentSmith.Domain/              # Entities, Value Objects, Exceptions
│   ├── AgentSmith.Contracts/           # Interfaces, Commands, Config contracts
│   ├── AgentSmith.Application/         # Use cases, pipeline, 22 command handlers
│   ├── AgentSmith.Infrastructure.Core/ # Config, detection, code map, registries
│   ├── AgentSmith.Infrastructure/      # Provider implementations (AI, Git, Tickets, Output)
│   ├── AgentSmith.Host/               # CLI entry point, Webhook listener
│   └── AgentSmith.Dispatcher/         # Chat gateway (Slack, Teams, K8s/Docker Jobs)
├── tests/
│   └── AgentSmith.Tests/              # 492 tests (xUnit, Moq, FluentAssertions)
├── config/
│   ├── agentsmith.example.yml         # Config template
│   └── skills/                        # Role definitions
│       ├── coding/                    # Architect, Backend Dev, Tester, DBA, Security, ...
│       ├── mad/                       # Philosopher, Dreamer, Realist, Devil's Advocate, Silencer
│       ├── legal/                     # Contract Analyst, Compliance Checker, Risk Assessor, ...
│       └── security/                  # Vuln Analyst, Auth Reviewer, Injection Checker, ...
├── .agentsmith/                       # Agent meta-files (auto-generated per repo)
│   ├── context.yaml                   # Project description + state tracking
│   ├── decisions.md                   # Why the agent made each decision
│   ├── coding-principles.md           # Detected coding conventions
│   ├── phases/                        # Phase documentation (done, active, planned)
│   └── runs/                          # Execution artifacts (plan.md + result.md)
├── .github/workflows/                 # CI/CD: Docker build + publish
├── Dockerfile                         # Agent runner image
├── Dockerfile.dispatcher              # Dispatcher image
└── docker-compose.yml                 # Full stack: agent, webhook, redis, dispatcher, ollama
```

---

## Roadmap

Everything up to phase 43 is done. That's 45 completed phases. Here's what's next.

**Done:** Core pipeline. Retry and resilience. Prompt caching and context compaction. Model registry with Scout agent. Multi-provider support (Claude, OpenAI, Gemini, Ollama). Cost tracking. Ticket writeback. Webhooks. Azure Repos, Jira, GitLab. Chat gateway with Slack and Teams. Auto-bootstrap. Code map generation. Coding principles detection. Multi-skill architecture. MAD discussions. Legal analysis pipeline. Decision logging. Security scanning with SARIF output. CI/CD with Docker Hub publishing. Ollama for local models with hybrid routing.

**Planned:** Multi-repo support (p23). PR review iteration (p25). Provider decomposition into independently deployable projects (p40b-d). Webhook expansion for all three platforms (p43e).

---

## Contributing

Contributions are welcome. Fork the repo, create a feature branch, follow the coding principles in `.agentsmith/coding-principles.md`, and open a pull request. For significant changes, open an issue first so we can discuss the approach before you write the code.

---

## License

MIT License. Copyright (c) 2026 Holger Leichsenring.

Use it. Modify it. Ship it. Just keep the license notice.

---

<p align="center">
  Built with Claude · Runs on .NET 8 · Ships via Docker · 492 tests and counting
</p>
