# Agent Smith

**Your AI. Your infrastructure. Your rules.**

Self-hosted AI orchestration · code · legal · security · workflows

[![Agent smith](/docs/agent-smith-logo-large-green.png)](logo)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com)
[![Docker](https://img.shields.io/badge/Docker-ready-blue.svg)](Dockerfile)

Agent Smith is an open-source framework for building and running AI-powered workflows. Define pipelines as chains of steps. Each step can spawn sub-agents, run multi-role discussions, call tools, or just move files around. The framework handles orchestration, tool calling, cost tracking, and delivery.

Everything runs on your infrastructure. Your API keys. Your data. No SaaS platform in between.

**[Documentation](https://code.agent-smith.org)** · **[Releases](https://github.com/holgerleichsenring/agent-smith/releases)** · **[Community](https://agent-smith.org)**

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

That's the standard bug fix pipeline. There are [seven more](https://code.agent-smith.org/pipelines/).

---

## Installation

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

All platforms (Linux x64/ARM64, macOS Intel/Apple Silicon, Windows) on the [Releases page](https://github.com/holgerleichsenring/agent-smith/releases). Full [installation guide](https://code.agent-smith.org/getting-started/installation/).

---

## Quick Start

```bash
export ANTHROPIC_API_KEY=sk-ant-...

# Fix a bug
agent-smith fix --ticket 42 --project my-api --headless

# Scan a live API
agent-smith api-scan --swagger https://api.example.com/swagger.json \
  --target https://api.example.com --output console,markdown

# Security scan a codebase
agent-smith security-scan --repo . --project my-api
```

See the [documentation](https://code.agent-smith.org) for configuration, CI/CD integration, AI provider setup, and more.

---

## License

MIT License. Copyright (c) 2026 Holger Leichsenring.

<p align="center">
  Built with Claude · Runs on .NET 8 · Ships via Docker · 567 tests and counting
</p>
