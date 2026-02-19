# Run Log 002: First Full Docker Run with Azure DevOps

**Date:** 2026-02-19
**Goal:** Run Agent Smith as a Docker container against Azure DevOps (AzureRepos + AzureDevOps tickets) for the first time - full 9/9 pipeline, headless mode, PR creation.
**Result:** ✅ Complete success. 9/9 pipeline steps passed. PR created on Azure Repos.

---

## 1. Setup

### 1.1 Existing Configuration

The `agent-smith-test` project was already defined in `config/agentsmith.yml`:

```yaml
agent-smith-test:
  source:
    type: AzureRepos
    url: https://dev.azure.com/holgerleichsenring/agent-smith-test/_git/agent-smith-test
    auth: token
  tickets:
    type: AzureDevOps
    organization: holgerleichsenring
    project: agent-smith-test
    auth: token
  agent:
    type: Claude
    model: claude-sonnet-4-20250514
  pipeline: fix-bug
```

### 1.2 Secrets in .env

```bash
ANTHROPIC_API_KEY=sk-ant-...
AZURE_DEVOPS_TOKEN=A8fc...
```

No `GITHUB_TOKEN` needed for this run - Azure DevOps only.

### 1.3 Create Test Ticket via REST API

Before running, an Azure DevOps work item was created via the REST API directly (no UI needed):

```bash
curl -s -u ":$AZURE_DEVOPS_TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -X POST \
  "https://dev.azure.com/holgerleichsenring/agent-smith-test/_apis/wit/workitems/\$Task?api-version=7.1" \
  -d '[
    {"op": "add", "path": "/fields/System.Title",
     "value": "Add a LICENSE file with MIT license text"},
    {"op": "add", "path": "/fields/System.Description",
     "value": "Create a LICENSE file in the repository root containing the standard MIT license text. Use the current year and placeholder author name. The file should be named LICENSE (no extension)."}
  ]'
```

**Result:** Work item `#54` created in project `agent-smith-test`.

This mirrors the predecessor instance's GitHub test (Issue #1: "Add a README.md") - a deliberately simple, self-contained task with no code dependencies.

---

## 2. Docker Build

The image was already cached from a previous build. Rebuild confirmed clean:

```bash
docker build -t agentsmith:latest .
# [+] Building 0.1s (23/23) FINISHED - all layers cached
```

Image size stayed well within the <500MB target.

---

## 3. Docker Run - Full Headless Execution

```bash
docker run --rm \
  -e ANTHROPIC_API_KEY="$ANTHROPIC_API_KEY" \
  -e AZURE_DEVOPS_TOKEN="$AZURE_DEVOPS_TOKEN" \
  -e GITHUB_TOKEN="$GITHUB_TOKEN" \
  -v $(pwd)/config:/app/config \
  -v ~/.ssh:/home/agentsmith/.ssh:ro \
  agentsmith:latest \
  --headless "fix #54 in agent-smith-test"
```

---

## 4. Full Pipeline Output

All 9 steps completed without error:

```
[1/9] FetchTicketCommand    → Ticket 54 fetched from AzureDevOps
[2/9] CheckoutSourceCommand → Cloning to /tmp/agentsmith/azuredevops/agent-smith-test/agent-smith-test
                              Checked out branch fix/54
[3/9] LoadCodingPrinciplesCommand → Loaded coding principles (3524 chars)
[4/9] AnalyzeCodeCommand    → Code analysis completed: 1 files found
[5/9] GeneratePlanCommand   → Plan: "Create a MIT LICENSE file at the repository root
                              with standard MIT license text, current year, and
                              placeholder author name" (1 step)
[6/9] ApprovalCommand       → Headless mode: auto-approving plan
[7/9] AgenticExecuteCommand → Scout: 1 relevant file discovered (6276 tokens)
                              Agent completed after 4 iterations
                              1 file changed
[8/9] TestCommand           → No test framework detected, skipping tests
[9/9] CommitAndPRCommand    → Committed and pushed: fix: Add a LICENSE file with MIT license text (#54)
                              PR created: https://dev.azure.com/holgerleichsenring/agent-smith-test/_git/agent-smith-test/pullrequest/4
                              Ticket 54 closed with summary
```

**Final output:** `Success: Pipeline completed successfully`

---

## 5. Agentic Loop Detail (Step 7)

The Scout + primary agent ran efficiently:

| Metric | Value |
|--------|-------|
| Scout model | `claude-haiku-4-5-20251001` |
| Relevant files discovered | 1 |
| Scout tokens used | 6,276 |
| Agent iterations | 4 (Scout) + 9 total loop iterations |
| Total input tokens | 7,978 |
| Total output tokens | 1,110 |
| Cache-create tokens | 1,564 |
| Cache-read tokens | 4,692 |
| Cache hit rate | 37.0% |
| Files changed | 1 (`LICENSE`) |

The task was minimal (1 existing file in repo, no complex codebase to analyze), so the agent completed in 4 iterations without hitting rate limits - in contrast to run-log-001 where a 129-file codebase caused rate limiting at iteration 6.

---

## 6. PR and Ticket

| Artifact | URL |
|----------|-----|
| Pull Request | https://dev.azure.com/holgerleichsenring/agent-smith-test/_git/agent-smith-test/pullrequest/4 |
| Work Item | https://dev.azure.com/holgerleichsenring/agent-smith-test/_workitems/edit/54 |

The ticket was automatically closed by the `CommitAndPRHandler` after PR creation (Phase 13 writeback).

---

## 7. Key Differences vs. Run Log 001

| Aspect | Run 001 (GitHub, dotnet run) | Run 002 (Azure DevOps, Docker) |
|--------|------------------------------|-------------------------------|
| Ticket provider | GitHub Issues | AzureDevOps Work Items |
| Source provider | GitHub (clone) | AzureRepos (clone) |
| Execution mode | `dotnet run` interactive | `docker run --headless` |
| Approval | Manual stdin pipe (`echo "y"`) | Auto-approved (headless mode) |
| Rate limit hit | Yes (iteration 6, 129 files) | No (1 file, 4 iterations) |
| Pipeline result | 7/9 (rate limited) | **9/9 complete** ✅ |
| PR created | No | **Yes** ✅ |
| Ticket closed | No | **Yes** ✅ |

---

## 8. Issues Found

None. First clean end-to-end run.

---

## 9. Next Steps

- [ ] Phase 18: Message Bus + Conversation State (Redis Streams)
- [ ] Run against a real-world ticket with actual code changes (multi-file)
- [ ] Test with OpenAI / Gemini provider instead of Claude