# Pipelines

Agent Smith ships with **8 pipeline presets** — pre-built sequences of command handlers that cover the most common AI orchestration workflows.

## Pipeline Overview

| Pipeline | CLI Command | Steps | What It Does |
|----------|------------|-------|-------------|
| **fix-bug** | `agent-smith fix` | 14 | Ticket → branch → code → test → PR |
| **fix-no-test** | `agent-smith fix --no-test` | 13 | Like fix-bug, skips the test step |
| **add-feature** | `agent-smith feature` | 16 | fix-bug + generate tests + generate docs |
| **security-scan** | `agent-smith security-scan` | 8 | Multi-role code security review with SARIF output |
| **api-security-scan** | `agent-smith api-scan` | 8 | Nuclei + Spectral + AI specialist panel on live APIs |
| **legal-analysis** | `agent-smith legal` | 7 | Contract review with 5 legal specialist roles |
| **mad-discussion** | `agent-smith mad` | 9 | Multi-agent design discussion with convergence |
| **skill-manager** | `agent-smith skill-manager` | 6 | Discover, evaluate, and install skills |
| **autonomous** | `agent-smith autonomous` | 9 | Observe project, write tickets autonomously |
| **init-project** | `agent-smith init` | 3 | Bootstrap `.agentsmith/` directory in a repo |

## How Pipelines Work

Every pipeline is an ordered list of **commands**. Each command has a matching **handler** that does the actual work. Commands share a `PipelineContext` — a key-value store that flows data between steps.

```
Pipeline: fix-bug
├── FetchTicket          → reads ticket from GitHub/AzDO/Jira/GitLab
├── CheckoutSource       → clones repo, creates branch
├── BootstrapProject     → detects language, framework, project type
├── LoadCodeMap          → generates navigable code map
├── LoadDomainRules      → loads coding standards from repo
├── LoadContext           → loads .agentsmith/context.yaml
├── AnalyzeCode          → scout agent maps relevant files
├── Triage               → selects specialist roles (coding pipeline)
├── GeneratePlan         → AI generates implementation plan
├── Approval             → waits for human OK (or runs headless)
├── AgenticExecute       → AI writes code in agentic loop
├── Test                 → runs test suite
├── WriteRunResult       → writes result.md with cost/token data
└── CommitAndPR          → commits, pushes, opens PR
```

## Dynamic Pipeline Expansion

Some commands insert follow-up steps at runtime. The **Triage** step inspects the problem and inserts `SkillRound` commands for each specialist role it selects. The **ConvergenceCheck** step evaluates whether all roles have met their convergence criteria — if not, it inserts another round.

```
Static pipeline:
  ... → Triage → ConvergenceCheck → CompileDiscussion → ...

After Triage expands:
  ... → Triage → SkillRound(vuln-analyst, r1) → SkillRound(auth-reviewer, r1)
      → ConvergenceCheck → CompileDiscussion → ...

After ConvergenceCheck finds objections:
  ... → ConvergenceCheck → SkillRound(auth-reviewer, r2)
      → ConvergenceCheck → CompileDiscussion → ...
```

## Custom Pipelines

You can define custom pipelines in `agentsmith.yml`:

```yaml
pipelines:
  my-custom-pipeline:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - BootstrapProjectCommand
      - AgenticExecuteCommand
      - CommitAndPRCommand
```

Then reference it in your project config:

```yaml
projects:
  my-project:
    pipeline: my-custom-pipeline
```

## Pipeline Categories

Agent Smith's pipelines fall into three categories:

**Coding pipelines** — fix-bug, fix-no-test, add-feature. These clone a repo, understand the code, write changes, test, and open a PR. They use an agentic loop with file I/O tools.

**Analysis pipelines** — security-scan, api-security-scan, legal-analysis. These assemble a panel of specialist roles, run multiple discussion rounds with convergence checking, and deliver structured output (SARIF, Markdown, console).

**Discussion pipelines** — mad-discussion. Multi-agent debates where specialist personas discuss a topic in rounds, converge on a position, and produce a compiled document.

## Next Steps

- [Fix Bug / Add Feature](fix-and-feature.md) — the coding pipelines
- [Security Scan](security-scan.md) — code security review
- [API Scan](api-scan.md) — live API scanning
- [Legal Analysis](legal-analysis.md) — contract review
- [MAD Discussion](mad-discussion.md) — multi-agent design debate
- [Skill Manager](skill-manager.md) — autonomous skill discovery and installation
- [Autonomous](autonomous.md) — agent-driven project improvement tickets
