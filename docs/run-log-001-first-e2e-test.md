# Run Log 001: First End-to-End Test

**Date:** 2026-02-16  
**Goal:** Run Agent Smith against itself - have it read a GitHub Issue from its own repo and generate code changes.  
**Result:** Pipeline reached Agentic Execution (step 7/9), successfully called Claude with tools before hitting API rate limit.

---

## 1. Setup

### 1.1 Push to GitHub

The repo had 5 phase commits locally but the GitHub remote was empty.

```bash
$ git push origin main

Enumerating objects: 196, done.
...
To https://github.com/holgerleichsenring/agent-smith.git
 * [new branch]      main -> main
```

### 1.2 Create Test Issue

Created GitHub Issue #1 as a test ticket for Agent Smith to work on:

```bash
$ gh issue create \
  --title "Add a README.md with project description" \
  --body "Create a README.md file for the Agent Smith project. It should include:
- Project name and short description (AI coding agent that processes tickets and generates code changes)
- How it works (ticket -> code analysis -> plan -> agentic execution -> PR)
- Prerequisites (.NET 8, API keys)
- Quick start / usage example
- Project structure overview
- License placeholder

Keep it concise and professional."

Creating issue in holgerleichsenring/agent-smith
https://github.com/holgerleichsenring/agent-smith/issues/1
```

### 1.3 Update Configuration

Changed `config/agentsmith.yml` to point at the agent-smith repo itself, using GitHub for both tickets and source:

```yaml
projects:
  agent-smith:
    source:
      type: GitHub
      url: https://github.com/holgerleichsenring/agent-smith
      auth: token
    tickets:
      type: GitHub
      url: https://github.com/holgerleichsenring/agent-smith
      auth: token
    agent:
      type: Claude
      model: claude-sonnet-4-20250514
    pipeline: fix-bug
    coding_principles_path: ./config/coding-principles.md
```

### 1.4 Environment Variables

```bash
export ANTHROPIC_API_KEY="sk-ant-api03-..."
export GITHUB_TOKEN=$(gh auth token)
```

---

## 2. Dry Run

First a dry run to verify intent parsing and config resolution:

```bash
$ dotnet run --project src/AgentSmith.Host -- --dry-run "fix #1 in agent-smith"

info: AgentSmith.Application.Services.RegexIntentParser[0]
      Parsed intent: Ticket=1, Project=agent-smith
Dry run - would execute:
  Project:  agent-smith
  Ticket:   #1
  Pipeline: fix-bug
  Commands:
    - FetchTicketCommand
    - CheckoutSourceCommand
    - LoadCodingPrinciplesCommand
    - AnalyzeCodeCommand
    - GeneratePlanCommand
    - ApprovalCommand
    - AgenticExecuteCommand
    - TestCommand
    - CommitAndPRCommand
```

Input `"fix #1 in agent-smith"` correctly parsed to TicketId=`1`, Project=`agent-smith`. All 9 pipeline commands resolved.

---

## 3. First Real Run - JSON Parse Error

```bash
$ dotnet run --project src/AgentSmith.Host -- --verbose "fix #1 in agent-smith"
```

### What worked (steps 1-4):

```
[1/9] FetchTicketCommand    -> Ticket 1 fetched from GitHub
[2/9] CheckoutSourceCommand -> Cloning to /tmp/agentsmith/holgerleichsenring/agent-smith
                               Checked out branch fix/1
[3/9] LoadCodingPrinciplesCommand -> Loaded coding principles (3524 chars)
[4/9] AnalyzeCodeCommand    -> Code analysis completed: 129 files found
```

### Where it failed (step 5):

```
[5/9] GeneratePlanCommand
fail: Handler GeneratePlanContext failed
      AgentSmith.Domain.Exceptions.ProviderException: Failed to parse plan response from Claude:
      '`' is an invalid start of a value. LineNumber: 0 | BytePositionInLine: 0.
```

### Root Cause

Claude returned the JSON wrapped in a Markdown code block:

````
```json
{ "summary": "...", "steps": [...] }
```
````

The `ParsePlan` method tried to parse this raw string as JSON, but the leading backticks caused a `JsonReaderException`.

### Fix

Added `StripMarkdownCodeBlock()` helper in `ClaudeAgentProvider.cs`:

```csharp
private static string StripMarkdownCodeBlock(string text)
{
    var trimmed = text.Trim();
    if (trimmed.StartsWith("```"))
    {
        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline >= 0)
            trimmed = trimmed[(firstNewline + 1)..];
    }
    if (trimmed.EndsWith("```"))
    {
        trimmed = trimmed[..^3].TrimEnd();
    }
    return trimmed;
}
```

Called before `JsonDocument.Parse()` in `ParsePlan()`.

---

## 4. Second Run - Approval Timeout

After the JSON fix, the pipeline advanced further:

```
[1/9] FetchTicketCommand    -> Ticket 1 fetched from GitHub
[2/9] CheckoutSourceCommand -> Checked out branch fix/1
[3/9] LoadCodingPrinciplesCommand -> Loaded coding principles (3524 chars)
[4/9] AnalyzeCodeCommand    -> 129 files found
[5/9] GeneratePlanCommand   -> Plan generated with 1 steps:
      "Create a comprehensive README.md file for the Agent Smith project"
[6/9] ApprovalCommand       -> Plan summary displayed, waiting for input...
      Approve this plan? (y/n):
```

### Problem

Running in a non-interactive shell context, `Console.ReadLine()` returned `null`, which the `ApprovalHandler` interpreted as rejection.

```
ApprovalContext failed: Plan rejected by user
```

### Workaround

Piped `"y"` into stdin:

```bash
$ echo "y" | dotnet run --project src/AgentSmith.Host -- --verbose "fix #1 in agent-smith"
```

---

## 5. Third Run - Agentic Execution (Rate Limited)

With auto-approve via pipe, the pipeline reached the agentic execution step:

```
[1/9] FetchTicketCommand    -> Ticket 1 fetched from GitHub
[2/9] CheckoutSourceCommand -> Cloning to /tmp/agentsmith/holgerleichsenring/agent-smith
                               Checked out branch fix/1
[3/9] LoadCodingPrinciplesCommand -> Loaded coding principles (3524 chars)
[4/9] AnalyzeCodeCommand    -> 129 files found
[5/9] GeneratePlanCommand   -> Plan: "Create a comprehensive README.md file at the
                               project root with project description, architecture
                               overview, prerequisites, usage instructions, and
                               project structure." (1 step)
[6/9] ApprovalCommand       -> Plan approved by user
[7/9] AgenticExecuteCommand -> Agentic loop started...
```

### The Agentic Loop in Action

Claude started working autonomously with tools:

```
dbug: Agentic loop iteration 1 -> Executing tool: list_files
dbug: Agentic loop iteration 2 -> Executing tool: read_file
dbug: Agentic loop iteration 3 -> Executing tool: read_file
dbug: Agentic loop iteration 4 -> Executing tool: read_file
dbug: Agentic loop iteration 5 -> Executing tool: read_file
dbug: Agentic loop iteration 6 -> RATE LIMITED
```

Claude was reading the project files to understand the codebase before writing the README. It listed files, then read several key files (likely `Program.cs`, config files, project structure) to gather information.

### Rate Limit Error

```
Anthropic.SDK.RateLimitsExceeded: This request would exceed your organization's
rate limit of 30,000 input tokens per minute
(org: f8326d03-d8cc-41ee-8c5a-68185a0ace38, model: claude-sonnet-4-20250514)
```

Each iteration of the agentic loop sends the full conversation history + tool results. After 5 iterations of reading files from a 129-file codebase, the accumulated token count exceeded the per-minute rate limit.

---

## 6. The Recursive Beauty

Agent Smith was working on **itself**:

1. Read a GitHub Issue from **its own repo**
2. Cloned **its own source code** into a temp directory
3. Analyzed **its own architecture** (129 files, .NET 8, Clean Architecture)
4. Asked Claude to create a plan **about itself**
5. Claude started reading **its own files** to write a README **about itself**

Peak recursion. The AI coding agent's first task was to document itself.

---

## 7. Issues Found & Fixed

| # | Issue | Root Cause | Fix |
|---|-------|-----------|-----|
| 1 | JSON parse error on plan response | Claude wraps JSON in markdown code blocks | `StripMarkdownCodeBlock()` strips ` ```json ... ``` ` wrapping |
| 2 | Approval rejected in non-interactive mode | `Console.ReadLine()` returns null | Piped `"y"` via stdin; future: add `--auto-approve` flag |
| 3 | Rate limit during agentic loop | 30k tokens/min limit, conversation grows with each tool call | Need retry logic with exponential backoff |

---

## 8. Next Steps

- [ ] Add retry logic with exponential backoff in `AgenticLoop` for rate limit errors
- [ ] Add `--auto-approve` CLI flag to skip the interactive approval step
- [ ] Re-run after rate limit cooldown to complete the full 9-step pipeline
- [ ] Verify the PR creation on GitHub (steps 8-9: Test + CommitAndPR)
