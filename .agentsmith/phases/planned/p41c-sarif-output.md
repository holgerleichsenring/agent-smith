# Phase 41c: SARIF Output Strategy & Platform Integration

## Goal

Security scan findings are delivered in SARIF format and uploaded to the
platform's security dashboard. A human-readable summary is posted as a PR/MR
comment. Depends on p41b being in place.

---

## IOutputStrategy Implementations

p41b defines `IOutputStrategy`. This phase adds:

### SarifOutputStrategy

Builds a valid SARIF 2.1.0 document from `CompileDiscussionCommand` output.

Finding → SARIF mapping:
- Severity HIGH → `level: error`
- Severity MEDIUM → `level: warning`
- Severity LOW → `level: note`
- File path + line numbers from structured skill output
- Rule IDs: `AS001`–`AS999` auto-generated per unique vulnerability type

```json
{
  "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
  "version": "2.1.0",
  "runs": [{
    "tool": { "driver": { "name": "Agent Smith Security", "rules": [...] } },
    "results": [{
      "ruleId": "AS001",
      "level": "error",
      "message": { "text": "SQL injection via unsanitized user input" },
      "locations": [{
        "physicalLocation": {
          "artifactLocation": { "uri": "src/Api/Controllers/UserController.cs" },
          "region": { "startLine": 47, "endLine": 52 }
        }
      }]
    }]
  }]
}
```

### MarkdownOutputStrategy

Renders findings as a Markdown table + summary. Used for `--output markdown`
and as the PR/MR comment body.

```markdown
## Agent Smith Security Review

Found 3 issues (1 HIGH, 2 MEDIUM)

| Severity | File | Line | Issue |
|----------|------|------|-------|
| 🔴 HIGH | src/Api/UserController.cs | 47 | SQL injection via unsanitized input |
| 🟡 MEDIUM | src/Auth/TokenService.cs | 23 | JWT secret without validation |
| 🟡 MEDIUM | src/Config/DbConfig.cs | 8 | Connection string logged on startup |

Cost: $0.043 | Model: claude-sonnet | Duration: 34s
```

### LocalFileOutputStrategy

Refactor of existing `DeliverOutputHandler` from Phase 42. Writes to outbox,
archives source. Becomes one of the keyed strategies.

---

## IPrCommentProvider — New Interface in Contracts

```csharp
// src/AgentSmith.Contracts/Providers/IPrCommentProvider.cs
public interface IPrCommentProvider
{
    Task PostCommentAsync(string prIdentifier, string markdown, CancellationToken ct);
}
```

Existing source providers implement additionally:
- `GitHubSourceProvider` → Octokit `PullRequest.CreateComment()`
- `GitLabSourceProvider` → API v4 `/merge_requests/{iid}/notes`
- `AzureReposSourceProvider` → GitHttpClient thread comments

Additive — no breaking change. `SarifOutputStrategy` gets `IPrCommentProvider`
injected to post the summary comment after SARIF upload.

---

## Platform Upload

### GitHub

```
POST /repos/{owner}/{repo}/code-scanning/sarifs
Body: { "commit_sha": "...", "ref": "refs/pull/42/head", "sarif": "<base64-gzip>" }
```

Requires `security_events: write` permission.

### GitLab

SARIF as CI artifact — write to `gl-sast-report.json` in workspace.
GitLab picks it up automatically if present.

**Customer requirement**: Add to `.gitlab-ci.yml`:
```yaml
agent-smith-security:
  artifacts:
    reports:
      sast: gl-sast-report.json
```

Document this in README / setup guide.

### Azure DevOps

No native SARIF dashboard. Default: post findings as Work Item comment
(structured markdown). SARIF file published as pipeline artifact (downloadable).

### CLI / Local

Write `findings.sarif` to current directory. Print human-readable summary
to stdout.

---

## Skill Output Format Requirement

Skills must emit structured findings for SARIF mapping. Add to
`security-principles.md` and all security skill YAMLs:

```
For each finding, include:
- severity: HIGH | MEDIUM | LOW
- file: relative path from repo root
- start_line: integer
- end_line: integer (optional)
- title: short description (max 80 chars)
- description: detailed explanation
- confidence: 1-10 (findings below 8 are discarded by false-positive-filter)
```

---

## Files to Create

- `src/AgentSmith.Contracts/Providers/IPrCommentProvider.cs`
- `src/AgentSmith.Infrastructure/Services/Output/SarifOutputStrategy.cs`
- `src/AgentSmith.Infrastructure/Services/Output/MarkdownOutputStrategy.cs`
- `src/AgentSmith.Infrastructure/Services/Output/LocalFileOutputStrategy.cs`
- Tests: SarifOutputStrategy, MarkdownOutputStrategy, IPrCommentProvider mocks

## Files to Modify

- `src/AgentSmith.Infrastructure/Services/Providers/Source/GitHubSourceProvider.cs` — implement IPrCommentProvider
- `src/AgentSmith.Infrastructure/Services/Providers/Source/GitLabSourceProvider.cs` — implement IPrCommentProvider
- `src/AgentSmith.Infrastructure/Services/Providers/Source/AzureReposSourceProvider.cs` — implement IPrCommentProvider
- `config/skills/security/*.yaml` — add structured output format requirement

---

## Definition of Done

- [ ] `SarifOutputStrategy` produces valid SARIF 2.1.0
- [ ] GitHub: SARIF uploaded, appears in Security tab
- [ ] GitLab: SARIF written as `gl-sast-report.json`, customer setup documented
- [ ] AzDO: findings posted as Work Item comment + SARIF as artifact
- [ ] CLI: `findings.sarif` written to disk + stdout summary
- [ ] PR/MR comment posted on all platforms via `IPrCommentProvider`
- [ ] Zero findings → comment says "No issues found" + empty SARIF uploaded
- [ ] `MarkdownOutputStrategy` renders findings table
- [ ] `LocalFileOutputStrategy` refactored from Phase 42 DeliverOutputHandler
- [ ] Skills emit structured output with file/line/severity/confidence
- [ ] Unit tests: SarifOutputStrategy, MarkdownOutputStrategy, severity mapping
- [ ] Integration test: end-to-end with mocked findings → valid SARIF file
