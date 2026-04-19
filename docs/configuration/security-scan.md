# Security Scan Configuration

Advanced configuration for the security-scan pipeline, including DAST scanning, automatic vulnerability fixing, and trend analysis.

## DAST (OWASP ZAP)

Dynamic Application Security Testing runs OWASP ZAP against a live application to find runtime vulnerabilities invisible in source code -- XSS, CSRF, auth bypass, header misconfiguration.

```yaml
projects:
  my-api:
    dast:
      enabled: true
      target: https://staging.my-api.example.com
      scan_type: baseline         # baseline | full-scan | api-scan
      auth:
        type: bearer
        token_env: DAST_TOKEN     # env var containing the auth token
```

| Scan Type | Duration | What It Finds |
|-----------|----------|---------------|
| `baseline` | ~2 min | Headers, TLS, passive findings (default) |
| `full-scan` | ~10 min | Active injection tests -- use on staging only |
| `api-scan` | ~5 min | REST API specific, uses OpenAPI spec |

ZAP runs as a Docker container using the same `docker cp` pattern as Nuclei (no volume mounts). Two dedicated LLM skills process ZAP findings:

- **dast-analyst** -- correlates ZAP findings with static analysis results, maps to OWASP Top 10
- **dast-false-positive-filter** -- removes known ZAP false positive patterns

## Auto-Fix

When enabled, Critical and High findings are automatically submitted as fix PRs. This is the only security tool that fixes vulnerabilities, not just reports them.

```yaml
projects:
  my-api:
    auto_fix:
      enabled: false               # explicit opt-in required
      severity_threshold: High     # Critical | High
      confirm_before_fix: true     # ask via dialogue before spawning fix
      max_concurrent: 3            # max parallel fix jobs
      excluded_patterns:
        - "**/*.generated.cs"
        - "**/Migrations/**"
```

The auto-fix flow:

1. Security scan completes, findings extracted
2. Critical/High findings grouped by file and category
3. If `confirm_before_fix: true`, the agent asks for approval via [Interactive Dialogue](../concepts/interactive-dialogue.md)
4. Separate fix jobs spawn (K8s jobs or Docker containers)
5. Each fix job runs the fix-bug pipeline with a security-specific system prompt
6. PRs are created with branch naming: `security-fix/cwe-{id}-{slug}`

!!! warning "Auto-fix is opt-in"
    `auto_fix.enabled` defaults to `false`. Fix jobs run as separate containers and do not block the security scan.

## Trend Analysis

Git-based security trend analysis tracks findings over time without any external database. Every security scan writes structured data to `result.md` with YAML frontmatter, and Git history serves as the time series.

```yaml
projects:
  my-api:
    security_trend:
      enabled: true
      lookback_scans: 10           # how many past scans to analyze
      commit_snapshot: true        # commit SARIF snapshot to default branch
```

### result.md Security Block

Each security scan adds a `security:` block to the run result frontmatter:

```yaml
security:
  findings_critical: 2
  findings_high: 7
  findings_medium: 14
  findings_retained: 9
  findings_auto_fixed: 3
  scan_types: [static, git-history, dependency, zap]
  new_since_last: 4
  resolved_since_last: 2
  top_categories: [secrets, injection, config]
```

### Trend Output

The trend appears in `result.md`, Slack notifications, and PR comments:

```
| Metric | Last Scan | This Scan | Delta |
|--------|-----------|-----------|-------|
| Critical | 3 | 2 | -1 |
| High | 8 | 7 | -1 |
| Retained | 11 | 9 | -2 |
| Auto-Fixed | 0 | 3 | +3 |
```

### CLI

```bash
# View trend for a project
agent-smith security-trend --project my-api

# Dry run -- show what would be analyzed without executing
agent-smith security-trend --project my-api --dry-run
```

## Confidence Calibration & False-Positive Rules

Security skills use a confidence calibration table and framework-specific false-positive rules defined in:

- `config/skills/observation-schema.md` — confidence bands (Low 0–30, Medium 31–69, High 70–100)
- `config/skills/security/security-principles.md` — exclusions and 12 framework-specific precedents (React XSS, GitHub Actions, env vars, etc.)
- `config/skills/api-security/api-security-principles.md` — API-specific exclusions and 8 precedents

These files govern all security skill roles. Edit them to adjust false-positive filtering for your codebase.

See also: [Security Scan Pipeline](../pipelines/security-scan.md) for the full pipeline documentation.
