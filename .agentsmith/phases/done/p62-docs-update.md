# Phase 62: Docs Site Update — New Phases p58–p61

> Extends the existing docs site (p53, docs.agent-smith.org / code.agent-smith.org)
> with all new concepts from phases 58–61.
> No structural change — extension of existing navigation.

**Timing:** After all other phases (p58–p61) are fully implemented.
Exception: `phase-workflow.md` can be created immediately (describes methodology, not features).

---

## Changes to `mkdocs.yml`

New pages integrated into existing navigation:

```yaml
nav:
  - Getting Started:
      # unchanged

  - Pipelines:
      - fix-bug / add-feature: pipelines/fix-bug.md
      - security-scan: pipelines/security-scan.md
      - api-scan: pipelines/api-scan.md
      - legal-analysis: pipelines/legal-analysis.md
      - mad-discussion: pipelines/mad-discussion.md
      - pr-review: pipelines/pr-review.md           # NEW (p25/p59)

  - Concepts:
      - Pipeline System: concepts/pipeline-system.md
      - Phases & Runs: concepts/phases-runs.md
      - Multi-Skill Architecture: concepts/multi-skill.md
      - Decision Logging: concepts/decision-logging.md
      - Cost Tracking: concepts/cost-tracking.md
      - Interactive Dialogue: concepts/interactive-dialogue.md   # NEW (p58)
      - Project Knowledge Base: concepts/knowledge-base.md      # NEW (p61)

  - Configuration:
      - agentsmith.yml Reference: configuration/agentsmith.md
      - Skills YAML Reference: configuration/skills.md
      - Tool Configuration: configuration/tools.md
      - Model Registry: configuration/model-registry.md
      - Webhook Configuration: configuration/webhooks.md        # NEW (p59)
      - Security Scan Config: configuration/security-scan.md    # NEW (p60)

  - AI Providers:
      # unchanged

  - Integrations:                                               # NEW (section renamed)
      - Slack: integrations/slack.md
      - PR Comments: integrations/pr-comments.md                # NEW (p59)
      - CLI: integrations/cli.md

  - CI/CD Integration:
      # unchanged

  - Deployment:
      # unchanged

  - Architecture:
      - Clean Architecture Layers: architecture/layers.md
      - Project Structure: architecture/structure.md
      - Extending Agent Smith: architecture/extending.md
      - Phase Workflow: architecture/phase-workflow.md           # NEW (p62)

  - Contributing:
      # unchanged
```

---

## New and Updated Pages

### 1. `docs/concepts/interactive-dialogue.md` — NEW (p58)

**Concept:** Ping-pong between agent and human — structured, auditable, across all channels.

```markdown
# Interactive Dialogue

Agent Smith conducts structured dialogues with the human during a pipeline —
not just at the start or end, but exactly when clarification is needed.

## Question Types

| Type | When | Example |
|---|---|---|
| Confirmation | Yes/No decision | "Should I proceed?" |
| Choice | Selection from options | "Which database strategy?" |
| Approval | Approval + optional comment | "Plan ready for approval" |
| FreeText | Free text input | "What branch name?" |
| Info | Information only, no input | "Deployment started" |

## Dialogue Trail

Every question and answer is logged in `result.md`...

## Channels

- **Slack** — Block Kit buttons, free text as next message
- **CLI** — Interactive prompt with timeout
- **PR comment** — `/approve` and `/reject` as commands

## Configuration

```yaml
agent:
  dialogue:
    timeout_seconds: 300
    default_on_timeout: yes
```

## When Does the Agent Ask?

The agent asks only when...
[Rules from p58 system prompt]
```

---

### 2. `docs/concepts/knowledge-base.md` — NEW (p61)

**Concept:** Karpathy pattern for codebases — the `.agentsmith/wiki/`.

```markdown
# Project Knowledge Base

Agent Smith compiles accumulated knowledge from all runs into a
readable, linked wiki under `.agentsmith/wiki/`.

## The Principle

Inspired by Andrej Karpathy's "LLM Knowledge Bases" (April 2026):
Instead of RAG and vector databases — simple markdown files in Git,
written and maintained by the LLM, read by humans.

```
.agentsmith/
  wiki/         ← LLM-compiled knowledge
    index.md
    decisions.md
    known-issues.md
    patterns.md
    concepts/
```

## Querying the Wiki

```
@agent-smith why do we use Repository Pattern?
@agent-smith what are the most common errors in PaymentService?
```

## Compiling the Wiki

```bash
agent-smith compile-wiki --project my-api
agent-smith wiki-lint --project my-api
```

## Health Checks

`wiki-lint` finds inconsistencies, gaps, and suggests new articles...
```

---

### 3. `docs/integrations/pr-comments.md` — NEW (p59)

```markdown
# PR Comment Integration

Agent Smith starts automatically when a PR comment begins with a
known command.

## Scenario A: Start a New Job

```
/agent-smith fix
/agent-smith fix #123 in my-api
/agent-smith security-scan
/agent-smith help
```

## Scenario B: Control a Running Job

When Agent Smith has posted a question in the PR:

```
/approve
/approve Please rename branch to feature/xyz
/reject The naming is wrong
```

## Webhook Setup

### GitHub
1. Repository Settings → Webhooks → Add webhook
2. Payload URL: `https://your-agent-smith/webhook`
3. Events: `Issue comments`, `Pull request review comments`
4. Secret: `${GITHUB_WEBHOOK_SECRET}`

## Security

`require_member: true` — only repository members can execute commands.
`allowed_pipelines` — only configured pipelines can be started.
```

---

### 4. `docs/configuration/webhooks.md` — NEW (p59)

```markdown
# Webhook Configuration

## Supported Platforms

| Platform | Event Types | Signature Method |
|---|---|---|
| GitHub | issue_comment, pull_request_review_comment | HMAC-SHA256 |

## Configuration

```yaml
webhooks:
  github_secret: ${GITHUB_WEBHOOK_SECRET}

projects:
  my-api:
    pr_commands:
      enabled: true
      require_member: true
      allowed_pipelines:
        - fix-bug
        - security-scan
```
```

---

### 5. `docs/configuration/security-scan.md` — NEW / EXTENDED (p60)

Extends existing security scan documentation with:

```markdown
## DAST (ZAP)

```yaml
security_scan:
  dast:
    enabled: true
    target_url: https://staging.example.com
    zap_mode: baseline
    timeout_seconds: 300
```

## Auto-Fix

```yaml
security_scan:
  auto_fix:
    enabled: true
    min_severity: high
    require_approval: true
    max_concurrent_fixes: 3
```

## Trend Analysis

```yaml
security_scan:
  trend:
    enabled: true
    lookback_scans: 4
    commit_snapshot: true
    trend_in_pr_comment: true
```
```

---

### 6. `docs/architecture/phase-workflow.md` — NEW (p62)

Meta-documentation: how phases are planned and implemented.

```markdown
# Phase Workflow

Agent Smith evolves itself through a structured phase process.

## What Is a Phase?

A phase is a bounded development increment with:
- Clear scope and definition of done
- Dependencies on other phases
- Implementation steps

Phases reside in `.agentsmith/phases/` as markdown files.

## Phase Status

| Status | Meaning |
|---|---|
| `done` | Implemented, documented in `context.yaml` |
| `active` | Currently in progress |
| `planned` | Specified, not yet implemented |

## Current State

### Implemented (p01–p56)
[Table from context.yaml]

### Planned (p58–p62)
| Phase | Name | Description |
|---|---|---|
| p58 | Interactive Dialogue | Ping-pong across all channels |
| p58b | Teams Integration | Adaptive Cards, Bot Service |
| p59 | PR Comment Webhook | New input type (GitHub) |
| p59b | GitLab MR Comments | GitLab webhook support |
| p59c | AzDO PR Comments | Azure DevOps webhook support |
| p60 | Security Enhancements | DAST, auto-fix, git trend |
| p61 | Project Knowledge Base | Karpathy pattern for codebases |

## Creating a Phase

New phase as markdown document in `.agentsmith/phases/planned/`:

```markdown
# Phase N: Title

## Goal
...

## Steps
...

## Definition of Done
- [ ] ...

## Dependencies
...
```
```

---

### 7. Updates to Existing Pages

**`docs/pipelines/security-scan.md`** — p54/p55/p56 extensions already
described in p56. Add p60a (ZAP), p60b (auto-fix), p60c (trend).

**`docs/concepts/phases-runs.md`** — mention `wiki/` as new part
of `.agentsmith/` structure (p61).

**`docs/concepts/decision-logging.md`** — link to `knowledge-base.md`,
explain how decisions.md is compiled into the wiki.

**`docs/pipelines/pr-review.md`** — connection to p59 PR comment trigger
and p58 dialogue answer.

---

## Summary: What Gets Created / Changed

| File | Status |
|---|---|
| `mkdocs.yml` | Extended |
| `docs/concepts/interactive-dialogue.md` | NEW |
| `docs/concepts/knowledge-base.md` | NEW |
| `docs/integrations/pr-comments.md` | NEW |
| `docs/configuration/webhooks.md` | NEW |
| `docs/configuration/security-scan.md` | Extended |
| `docs/architecture/phase-workflow.md` | NEW |
| `docs/pipelines/security-scan.md` | Extended |
| `docs/pipelines/pr-review.md` | Extended |
| `docs/concepts/phases-runs.md` | Extended |
| `docs/concepts/decision-logging.md` | Extended |

**Total:** 5 new pages, 6 extended pages

---

## Definition of Done

- [ ] `mkdocs.yml` navigation contains all new pages
- [ ] All 5 new pages created with complete content
- [ ] All 6 existing pages extended with new phase content
- [ ] Internal links between new pages correct
- [ ] `mkdocs build` without errors
- [ ] `mkdocs serve` — all pages render correctly
- [ ] No broken links (`mkdocs build --strict`)

## Dependencies

Conceptual: p58, p59, p60, p61 must be fully implemented first
Technical: p53 (docs site) must be deployed (already done)
