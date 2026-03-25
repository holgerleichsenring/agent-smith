# First Bug Fix

Fix a bug from ticket to pull request in one command.

## Prerequisites

- Agent Smith [installed](installation.md)
- An AI provider API key (e.g. `ANTHROPIC_API_KEY`)
- A GitHub/Azure DevOps/Jira/GitLab ticket to fix
- Git credentials configured (SSH key or token)

## 1. Create a Configuration

Create `agentsmith.yml`:

```yaml
projects:
  my-api:
    source:
      type: GitHub
      url: https://github.com/your-org/your-repo
      auth: token
    tickets:
      type: GitHub
      url: https://github.com/your-org/your-repo
      auth: token
    agent:
      type: Claude
      model: claude-sonnet-4-20250514
    pipeline: fix-bug

secrets:
  github_token: ${GITHUB_TOKEN}
  anthropic_api_key: ${ANTHROPIC_API_KEY}
```

## 2. Run It

```bash
export ANTHROPIC_API_KEY=sk-ant-...
export GITHUB_TOKEN=ghp_...

agent-smith fix --ticket 42 --project my-api --config agentsmith.yml --headless
```

## What Happens

Agent Smith runs 13 steps:

1. **FetchTicket** — reads ticket #42 from GitHub
2. **CheckoutSource** — clones the repo, creates `fix/42` branch
3. **BootstrapProject** — detects language, framework, conventions
4. **LoadCodeMap** — generates a navigable code map
5. **LoadDomainRules** — loads coding standards from `.agentsmith/coding-principles.md`
6. **LoadContext** — loads project context from `.agentsmith/context.yaml`
7. **AnalyzeCode** — scout agent identifies relevant files
8. **GeneratePlan** — AI writes a step-by-step plan
9. **Approval** — skipped in `--headless` mode
10. **AgenticExecute** — AI writes code using tools (read, write, list, shell)
11. **Test** — runs your test suite; if tests fail, the agent retries
12. **WriteRunResult** — writes `result.md` with token usage and cost data
13. **CommitAndPR** — commits, pushes, opens PR

## Output

The PR includes:

- The code changes
- A `result.md` with cost tracking and decision log
- The ticket is updated with the PR link

## Interactive Mode

Without `--headless`, Agent Smith pauses at step 9 and shows you the plan. You approve, request changes, or cancel before any code is written.

```bash
agent-smith fix --ticket 42 --project my-api --config agentsmith.yml
```

## Via Chat

If you have the Dispatcher running with Slack/Teams integration:

```
fix #42 in my-api
```

Agent Smith spawns an ephemeral container, runs the pipeline, and streams progress back to your channel.
