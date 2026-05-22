<img src="agent-smith-logo-large-green.png" alt="Agent Smith" style="max-width: 600px;">

**Tickets become PRs.** Self-hosted, multi-repo CI for AI pipelines. One ticket in, N pull requests out — running on your own infrastructure, calling your own AI provider, no SaaS in between.

---

## The lifecycle

![Lifecycle: ticket → orchestrator → sandboxes → pull requests → resolved](assets/lifecycle.svg)

One ticket lands in the orchestrator. Agent Smith spawns **one sandbox per repository** in the project, each running its own toolchain image (`dotnet/sdk:8.0`, `node:20`, `alpine:3`, anything you configure). The orchestrator holds one plan and one agent conversation across every repo; tool calls dispatch by path prefix. Each sandbox produces one pull request. The ticket is written back as resolved with every PR link cross-referenced.

---

## What a fix-bug run looks like

The 13 user-visible steps of the [`fix-bug` pipeline](pipelines/fix-and-feature.md). Plumbing (pipeline-name init, bootstrap probes, skill loaders, empty-plan checks) runs between these but isn't shown.

```
You:           "fix #54 in todo-list"
               ↓
 [ 1/13] FetchTicket          → Reads ticket from Azure DevOps / GitHub / Jira / GitLab
 [ 2/13] CheckoutSource       → Clones every repo in the project, creates branch agentsmith/ticket-54
 [ 3/13] LoadContext          → Loads each repo's .agentsmith/context.yaml
 [ 4/13] LoadCodingPrinciples → Loads each repo's coding-principles.md
 [ 5/13] AnalyzeCode          → Scout agent maps each repo, identifies relevant files
 [ 6/13] Triage               → Picks the right skill roster for this ticket
 [ 7/13] GeneratePlan         → AI generates a step-by-step implementation plan (multi-role)
 [ 8/13] Approval             → Shows plan, waits for your OK (or runs headless)
 [ 9/13] AgenticExecute       → AI writes the code per repo, iterating with tools
 [10/13] RunReviewPhase       → Multi-role review against the plan
 [11/13] RunVerifyPhase       → Evidence-grounded verifiers (build/test/static analysis)
 [12/13] Test                 → Runs each repo's test suite
 [13/13] CommitAndPR          → One PR per repo with changes, cross-linked
         PrCrossLink          → Sibling-PR body markers replaced with real URLs
               ↓
Agent Smith:   "4 pull requests opened. Ticket #54 → Done."
```

That's the standard bug fix. There are [seven more pipelines](pipelines/index.md).

---

## Quick Links

| | |
|---|---|
| **[Installation](getting-started/installation.md)** — Binary, Docker, or source | **[First Bug Fix](getting-started/first-bug-fix.md)** — From ticket to PR in 5 minutes |
| **[API Security Scan](getting-started/first-api-scan.md)** — Scan a live API | **[Pipelines](pipelines/index.md)** — All seven pipeline presets |
| **[Configuration](configuration/agentsmith-yml.md)** — agentsmith.yml reference | **[Multi-Repo Projects](concepts/multi-repo-pipelines.md)** — One ticket, N pull requests |
| **[AI Providers](providers/index.md)** — Claude, GPT-4, Gemini, Ollama, Groq | **[CI/CD Integration](cicd/index.md)** — Azure DevOps, GitHub Actions, GitLab |
| **[Architecture](architecture/index.md)** — Clean Architecture deep-dive | **[Design System](DESIGN.md)** — Tokens, components, swap-tomorrow contract |

---

## Pipelines at a Glance

| Pipeline | What it does |
|----------|-------------|
| **fix-bug** | Ticket → branch → code → review → verify → test → PR (per repo) |
| **add-feature** | Same flow, plus generated tests and docs |
| **security-scan** | Multi-role code security review (read-only) |
| **api-security-scan** | Nuclei + Spectral + AI panel against a live API |
| **legal-analysis** | Contract review with five legal specialists |
| **mad-discussion** | Multi-agent design discussion |
| **init-project** | Bootstrap `.agentsmith/` for each repo in a project |
| **autonomous** | Open-ended operator-driven flow |
| **skill-manager** | Author / lint / validate skills |

---

## License

MIT License. Copyright (c) 2026 Holger Leichsenring.

---

**[CodingSoul Blog](https://codingsoul.org)** — Posts about building Agent Smith · **[Community](https://github.com/holgerleichsenring/agent-smith/issues)** — Discuss, ask questions, share your setups
