# Phase 14: GitHub Action - Implementation Details

## Overview
GitHub Actions workflow that triggers when the `agent-smith` label is added to an issue.
Builds Agent Smith from source, runs it in headless mode against the labeled issue.

---

## Workflow File

`.github/workflows/agent-smith.yml`

### Trigger
```yaml
on:
  issues:
    types: [labeled]

jobs:
  agent-smith:
    if: github.event.label.name == 'agent-smith'
```

Only runs when the specific label is added. Other labels are ignored.

### Steps
1. **Checkout** - `actions/checkout@v4`
2. **Setup .NET** - `actions/setup-dotnet@v4` with 8.0.x
3. **Build** - `dotnet build --configuration Release`
4. **Run** - `dotnet run` with `--headless` and issue number from event context

### Environment Variables
```yaml
env:
  GITHUB_TOKEN: ${{ secrets.AGENT_SMITH_TOKEN || secrets.GITHUB_TOKEN }}
  ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
  OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
  GEMINI_API_KEY: ${{ secrets.GEMINI_API_KEY }}
```

### Input Construction
```bash
"fix #${{ github.event.issue.number }} in ${{ github.event.repository.name }}"
```

---

## Required Secrets
- `ANTHROPIC_API_KEY` (or `OPENAI_API_KEY` / `GEMINI_API_KEY` depending on config)
- `AGENT_SMITH_TOKEN` - GitHub PAT with `repo` + `issues` permissions
  - Falls back to `GITHUB_TOKEN` if not set
  - Note: `GITHUB_TOKEN` cannot trigger other workflows on the created PR

---

## Timeout
`timeout-minutes: 30` - prevents runaway costs on infinite loops.
