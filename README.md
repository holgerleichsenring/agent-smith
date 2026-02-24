# Agent Smith

[![Agent smith](/docs/agent-smith-logo-large.png)](logo)

An open-source, self-hosted AI coding agent that reads your tickets and ships the code.

You write: `fix #65 in todo-list`
Agent Smith reads the ticket, clones the repo, analyzes the codebase, writes a plan, executes it with Claude (or OpenAI, or Gemini), runs the tests, and opens a Pull Request — all without you touching a keyboard.

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
 [5/13] LoadCodingPrinciples → Loads your coding standards
 [6/13] LoadContext           → Loads project context (.agentsmith/context.yaml)
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

It is not a code completion tool. It is not a chatbot.
It is an autonomous agent that takes a ticket from `New` to `Pull Request` — by itself.

---

## Key Features

### Multi-Provider
Every piece is swappable via config. No vendor lock-in.

| Category | Providers |
|----------|-----------|
| **Ticket Systems** | Azure DevOps, GitHub Issues, Jira, GitLab Issues |
| **Source Control** | GitHub, Azure Repos, GitLab, Local |
| **AI Agents** | Claude (Anthropic), GPT-4 (OpenAI), Gemini (Google) |

### Agentic Loop
The AI doesn't just generate code once and call it done. It runs in a loop with real tools:
- `read_file` — reads any file in the repo
- `write_file` — writes changes
- `list_files` — explores the directory tree
- `run_command` — executes shell commands (tests, linters, build)

It iterates, reads its own output, corrects mistakes, and only stops when it's satisfied.

### Scout Agent
Before the main agent starts, a lightweight Scout agent (Haiku by default) maps the codebase and identifies relevant files. This saves tokens and keeps the main agent focused.

### Cost Tracking
Every run writes a `result.md` with YAML frontmatter containing token usage and cost per phase:
```yaml
---
ticket: "#57 — GET /todos returns 500 when database is empty"
date: 2026-02-24
result: success
duration_seconds: 50
tokens:
  input: 17164
  output: 2011
  cache_read: 11790
  total: 30965
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

Machine-parseable (queryable via `yq`) and human-readable.

### Auto-Bootstrap
On first run against a new repo, Agent Smith auto-detects the project type, coding conventions, and architecture — then generates `.agentsmith/context.yaml`, `coding-principles.md`, and a code map. No manual setup needed.

### Prompt Caching
System prompts and coding principles are cached at the Anthropic level, reducing costs significantly on repeated runs against the same codebase.

### Context Compaction
Long agentic runs automatically compact the conversation context before hitting token limits. A summarization model (Haiku) distills history, keeping the full context window available.

### Resilience
All API calls are wrapped with Polly retry logic — exponential backoff, jitter, configurable limits. Rate limit errors and transient failures are handled gracefully without crashing.

### Pipeline Presets
Multiple built-in pipelines for different workflows:

| Preset | Steps | Use Case |
|--------|-------|----------|
| `fix-bug` | 13 | Standard bug fix with tests |
| `fix-no-test` | 12 | Bug fix without test step |
| `add-feature` | 14 | Feature with test generation + docs |
| `init-project` | 3 | Bootstrap a new repo |

### Headless Mode
Run fully unattended in CI/CD or K8s. `--headless` auto-approves plans and never blocks on stdin.

### Webhook / Server Mode
Run as an HTTP server that listens for GitHub or Azure DevOps webhooks. Label a ticket `agent-smith` → the agent starts automatically.

### Chat Gateway
Talk to Agent Smith from Slack or Teams:
- `fix #65 in todo-list` → spawns a K8s Job, posts real-time progress to your channel
- `list tickets in todo-list` → lists open tickets instantly
- `create ticket "Add logging" in todo-list` → creates the ticket and returns its ID
- Agent asks questions? You get buttons to click. It waits for your answer.

---

## Architecture

```
┌──────────────────────────────────────────────────────┐
│                  AgentSmith.Host                      │
│   CLI (System.CommandLine) + WebhookListener          │
└──────────────┬───────────────────────────────────────┘
               │
┌──────────────▼───────────────────────────────────────┐
│               AgentSmith.Application                  │
│   ProcessTicketUseCase → PipelineExecutor             │
│   13 Command Handlers (one per pipeline step)         │
└──────────────┬───────────────────────────────────────┘
               │
┌──────────────▼───────────────────────────────────────┐
│             AgentSmith.Infrastructure                 │
│   Providers: Claude / OpenAI / Gemini                 │
│   Tickets:   AzureDevOps / GitHub / Jira / GitLab    │
│   Source:    AzureRepos / GitHub / GitLab / Local     │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│             AgentSmith.Dispatcher                     │
│   ASP.NET Core Minimal API                           │
│   Slack / Teams Adapters                             │
│   Redis Streams Message Bus                          │
│   K8s / Docker Job Spawner                           │
└──────────────────────────────────────────────────────┘
```

Clean Architecture. Every layer depends only inward. Every provider is behind an interface. No God classes. No hardcoded values.

---

## Quick Start

### Prerequisites

- Docker
- An AI provider API key (Anthropic, OpenAI, or Google)
- Tokens for your ticket system and source control

### 1. Clone and configure

```bash
git clone https://github.com/holgerleichsenring/agent-smith.git
cd agent-smith
cp config/agentsmith.example.yml config/agentsmith.yml
```

Edit `config/agentsmith.yml` and define your project:

```yaml
projects:
  my-project:
    source:
      type: GitHub
      url: https://github.com/yourorg/your-repo
      auth: token
    tickets:
      type: GitHub
      url: https://github.com/yourorg/your-repo
      auth: token
    agent:
      type: Claude
      model: claude-sonnet-4-20250514
      pricing:
        models:
          claude-sonnet-4-20250514:
            input_per_million: 3.0
            output_per_million: 15.0
            cache_read_per_million: 0.30
    pipeline: fix-bug
    coding_principles_path: .agentsmith/coding-principles.md

secrets:
  github_token: ${GITHUB_TOKEN}
  anthropic_api_key: ${ANTHROPIC_API_KEY}
```

### 2. Create a `.env` file

```bash
ANTHROPIC_API_KEY=sk-ant-...
GITHUB_TOKEN=ghp_...
# AZURE_DEVOPS_TOKEN=...
# OPENAI_API_KEY=...
# GEMINI_API_KEY=...
```

### 3. Sync secrets to Kubernetes

The K8s Jobs spawned by the Dispatcher need your tokens as a Kubernetes Secret.
Run this once after setup and whenever you change `.env`:

```bash
chmod +x apply-k8s-secret.sh
./apply-k8s-secret.sh
```

This creates (or updates) the `agentsmith-secrets` Secret in the `default` namespace.
For a different namespace, pass `--namespace <ns>`.

> **Note:** The secret sets `redis-url` to `redis:6379` by default (the in-cluster
> Redis service). Override via `REDIS_URL` in your `.env` if Redis runs elsewhere.

### 4. Run

```bash
# Build the image
docker build -t agentsmith:latest .

# Fix a ticket
docker run --rm \
  --env-file .env \
  -v $(pwd)/config:/app/config \
  -v ~/.ssh:/home/agentsmith/.ssh:ro \
  agentsmith:latest \
  --headless "fix #1 in my-project"
```

### 5. Or use Docker Compose

```bash
# One-shot run
docker compose run --rm agentsmith --headless "fix #1 in my-project"

# Webhook server mode (GitHub / Azure DevOps webhooks)
docker compose up -d agentsmith-server
```

---

## Configuration Reference

### Agent Options

```yaml
agent:
  type: Claude          # Claude | OpenAI | Gemini
  model: claude-sonnet-4-20250514
  retry:
    max_retries: 5
    initial_delay_ms: 2000
    backoff_multiplier: 2.0
    max_delay_ms: 60000
  cache:
    is_enabled: true
    strategy: automatic  # automatic | fine-grained | none
  compaction:
    is_enabled: true
    threshold_iterations: 8
    max_context_tokens: 80000
    keep_recent_iterations: 3
    summary_model: claude-haiku-4-5-20251001
  models:
    scout:
      model: claude-haiku-4-5-20251001
      max_tokens: 4096
    primary:
      model: claude-sonnet-4-20250514
      max_tokens: 8192
    planning:
      model: claude-sonnet-4-20250514
      max_tokens: 4096
    summarization:
      model: claude-haiku-4-5-20251001
      max_tokens: 2048
  pricing:
    models:
      claude-sonnet-4-20250514:
        input_per_million: 3.0
        output_per_million: 15.0
        cache_read_per_million: 0.30
      claude-haiku-4-5-20251001:
        input_per_million: 0.80
        output_per_million: 4.0
        cache_read_per_million: 0.08
```

### Ticket Providers

**GitHub Issues:**
```yaml
tickets:
  type: GitHub
  url: https://github.com/yourorg/your-repo
  auth: token
```

**Azure DevOps:**
```yaml
tickets:
  type: AzureDevOps
  organization: yourorg
  project: YourProject
  auth: token
```

**Jira:**
```yaml
tickets:
  type: Jira
  url: https://yourcompany.atlassian.net
  project: PROJ
  auth: token
```

**GitLab:**
```yaml
tickets:
  type: GitLab
  url: https://gitlab.com
  project: yourorg/your-repo
  auth: token
```

### Source Providers

```yaml
source:
  type: GitHub       # GitHub | AzureRepos | GitLab | Local
  url: https://github.com/yourorg/your-repo
  auth: token        # token | ssh
```

---

## CLI Reference

```
agentsmith [input] [options]

Arguments:
  input    Ticket reference, e.g. "fix #123 in my-project"

Options:
  --config <path>      Path to agentsmith.yml [default: config/agentsmith.yml]
  --dry-run            Show pipeline without executing
  --headless           Auto-approve plans (no interactive prompts)
  --verbose            Enable debug logging
  --server             Start as webhook listener
  --port <int>         Webhook listener port [default: 8080]
  --job-id <id>        Redis Streams job ID (K8s job mode)
  --redis-url <url>    Redis connection URL (K8s job mode)
```

### Examples

```bash
# Dry run - see what would happen
agentsmith --dry-run "fix #42 in todo-list"

# Interactive run with approval step
agentsmith "fix #42 in todo-list"

# Fully headless (CI/CD, K8s)
agentsmith --headless "fix #42 in todo-list"

# Webhook server
agentsmith --server --port 8080
```

---

## Authentication

All secrets are injected as environment variables. Never put tokens in `agentsmith.yml`.

| Variable | Used For |
|----------|----------|
| `ANTHROPIC_API_KEY` | Claude models |
| `OPENAI_API_KEY` | GPT-4 models |
| `GEMINI_API_KEY` | Gemini models |
| `GITHUB_TOKEN` | GitHub tickets + source |
| `AZURE_DEVOPS_TOKEN` | Azure DevOps tickets + Azure Repos |
| `GITLAB_TOKEN` | GitLab tickets + source |
| `JIRA_TOKEN` | Jira tickets |
| `JIRA_EMAIL` | Jira authentication |

For SSH-based git operations, mount your SSH key:
```bash
-v ~/.ssh:/home/agentsmith/.ssh:ro
```

---

## Coding Principles

Agent Smith loads your coding principles at runtime and injects them into every AI prompt. This means the generated code follows *your* standards — not some generic defaults.

On first run (`init-project` pipeline), Agent Smith auto-detects your conventions from the codebase and generates `.agentsmith/coding-principles.md`. You can also define them manually:
- Line length limits
- Naming conventions
- Architecture patterns
- Frameworks to use (or avoid)
- Test requirements

The AI reads this file before writing a single line of code.

---

## Webhook Setup

### GitHub

Create a webhook in your repository settings:
- **Payload URL:** `https://your-server:8080/webhook`
- **Content type:** `application/json`
- **Events:** `Issues`

Label any issue with `agent-smith` → the agent starts automatically.

### Azure DevOps

Configure a Service Hook on `Work item updated` events pointing to your webhook URL.

---

## Slack / Teams Integration

Talk to Agent Smith directly from your chat:

```
fix #65 in todo-list
→ Starting Agent Smith for ticket #65 in todo-list...
→ [1/13] FetchTicketCommand...
→ [2/13] CheckoutSourceCommand...
→ Should I write unit tests? [Yes] [No]
→ [10/13] AgenticExecuteCommand...
→ Done! Pipeline completed successfully · View Pull Request
```

```
list tickets in todo-list
→ Open tickets in todo-list (5 total):
  • #65 — Fix login timeout [Active]
  • #66 — Add export CSV [New]
  ...
```

Each `fix` request runs in its own ephemeral Kubernetes Job (or Docker container). Progress, questions and results stream back to your channel in real time via Redis Streams.

See [docs/slack-setup.md](docs/slack-setup.md) for the full setup guide.

---

## Project Structure

```
agent-smith/
├── src/
│   ├── AgentSmith.Domain/          # Entities, Value Objects, Exceptions
│   ├── AgentSmith.Contracts/       # Interfaces, Commands, Config contracts
│   ├── AgentSmith.Application/     # Use cases, pipeline, command handlers
│   ├── AgentSmith.Infrastructure/  # Provider implementations
│   ├── AgentSmith.Host/            # CLI entry point, Webhook listener
│   └── AgentSmith.Dispatcher/      # Chat gateway (Slack, Teams, K8s/Docker Jobs)
├── tests/
│   └── AgentSmith.Tests/           # 344 tests (xUnit, Moq, FluentAssertions)
├── .agentsmith/                    # Agent meta-files (auto-generated per repo)
│   ├── context.yaml                # Project description + state tracking
│   ├── code-map.yaml               # LLM-generated code map
│   ├── coding-principles.md        # Detected coding conventions
│   ├── phases/                     # Phase documentation
│   │   ├── done/                   # Completed phases
│   │   ├── active/                 # Currently active (max 1)
│   │   └── planned/               # Upcoming phases
│   └── runs/                       # Execution artifacts (plan.md + result.md)
├── config/
│   └── agentsmith.example.yml      # Config template
├── docs/                           # Run logs, setup guides
├── Dockerfile
├── Dockerfile.dispatcher
└── docker-compose.yml
```

---

## Roadmap

- [x] Core pipeline, CLI, Docker (p01-p05)
- [x] Retry, caching, context compaction (p06-p08)
- [x] Model registry, Scout agent, container hardening (p09-p10)
- [x] OpenAI + Gemini providers, cost tracking (p11-p12)
- [x] Ticket writeback, Webhook trigger (p13-p14)
- [x] Azure Repos, Jira, GitLab providers (p15-p17)
- [x] Chat Gateway: Slack/Teams, Redis Streams, K8s/Docker Jobs (p18-p20)
- [x] Code quality, auto-bootstrap, code map generation (p21-p22, p24)
- [x] Coding principles detection, `.agentsmith/` directory (p26, p28)
- [x] Structured command UI: Slack modals, slash commands (p27)
- [x] Init project command, systemic fixes, orphan detection (p29-p31)
- [x] Architecture cleanup: ILlmClient abstraction (p32)
- [x] Run cost data in result.md with YAML frontmatter (p33)
- [ ] Multi-repo support (p23)
- [ ] PR review iteration (p25)

---

## Contributing

Contributions are welcome. Please:

1. Fork the repository
2. Create a feature branch
3. Follow the coding principles in `.agentsmith/coding-principles.md`
4. Open a Pull Request with a clear description

For significant changes, open an issue first to discuss the approach.

---

## License

MIT License

Copyright (c) 2026 Holger Leichsenring

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

---

<p align="center">
  Built with Claude · Runs on .NET 8 · Ships via Docker
</p>
