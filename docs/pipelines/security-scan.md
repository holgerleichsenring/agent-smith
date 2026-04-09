# Security Scan

The **security-scan** pipeline performs a multi-layered code security review combining static pattern matching, git history analysis, dependency auditing, and a multi-role AI specialist panel. It assembles 9 specialist skills, runs a structured discussion with convergence checking, and delivers findings in SARIF, Markdown, or console format.

## Pipeline Steps

The pipeline has 18 base steps, with dynamic expansion during the skill rounds phase.

| # | Command | What It Does |
|---|---------|-------------|
| 1 | CheckoutSource | Clones repo, optionally scopes to a PR diff or branch |
| 2 | BootstrapProject | Detects language, framework, dependencies |
| 3 | LoadDomainRules | Loads `security-principles.md` with exclusion rules |
| 4 | StaticPatternScan | Runs 91 regex patterns across 6 categories against source files |
| 5 | GitHistoryScan | Scans last 500 commits for secrets in git history via LibGit2Sharp |
| 6 | DependencyAudit | Runs `npm audit` / `pip-audit` / `dotnet audit` + structural checks |
| 7 | SpawnZap | Runs OWASP ZAP DAST scan (skips if `dast.enabled: false`) |
| 8 | SecurityTrend | Computes trend from previous SARIF snapshots |
| 9 | CompressSecurityFindings | Groups findings by category, creates skill-specific slices |
| 10 | LoadSkills | Loads 9 security specialist skills from `config/skills/security/` |
| 11 | AnalyzeCode | Scout agent maps file structure and dependency graph |
| 12 | SecurityTriage | AI selects relevant specialists, mandatory false-positive-filter |
| 13 | ConvergenceCheck | Runs skill rounds until consensus (dynamic expansion) |
| 14 | CompileDiscussion | Consolidates all findings into a final report |
| 15 | ExtractFindings | Converts discussion output to structured Finding records |
| 16 | DeliverFindings | Writes output in the requested format(s) |
| 17 | SecuritySnapshotWrite | Persists SARIF snapshot for trend history |
| 18 | SpawnFix | Spawns fix jobs for Critical/High findings (skips if `auto_fix.enabled: false`) |

!!! info "Dynamic expansion"
    Step 12-13 are dynamic. `SecurityTriage` inserts `SecuritySkillRound` commands for each selected specialist, plus a `ConvergenceCheck`. If specialists disagree, `ConvergenceCheck` inserts additional rounds until consensus or the max round limit is reached (default: 3).

## Static Pattern Scan

The `StaticPatternScan` step runs 91 regex patterns organized into 6 categories. Patterns are loaded from YAML files under `config/patterns/`:

| Category | Patterns | Examples |
|----------|----------|----------|
| **secrets** | 27 | AWS keys, GitHub tokens, private keys, connection strings |
| **injection** | 16 | SQL injection, command injection, XPath, template injection |
| **ssrf** | 12 | URL construction from user input, DNS rebinding vectors |
| **config** | 15 | Debug mode enabled, permissive CORS, missing security headers |
| **compliance** | 10 | PII logging, missing encryption, weak hashing algorithms |
| **ai-security** | 11 | Prompt injection, unsafe deserialization of model output, API key in prompts |

Pattern files are extensible -- add custom YAML files to `config/patterns/` and they are automatically picked up. See [Tool Configuration](../configuration/tools.md#pattern-definition-files) for the pattern file format.

## Git History Scan

The `GitHistoryScan` step uses LibGit2Sharp to scan the last 500 commits for secrets that may have been committed and later removed. Findings from git history are automatically marked as **CRITICAL** severity because the secret has been exposed in the repository history even if it no longer exists in the current codebase.

When a secret is detected, the scanner identifies the secret provider (AWS, GitHub, Stripe, etc.) and includes a **revoke URL** in the finding so teams can immediately rotate the compromised credential.

## Dependency Audit

The `DependencyAudit` step runs language-specific audit tools and performs structural checks:

- **npm audit** for Node.js projects
- **pip-audit** for Python projects
- **dotnet audit** for .NET projects
- **Structural checks**: missing lockfiles, wildcard version ranges, deprecated packages

## Finding Compression

The `CompressSecurityFindings` step groups raw findings by category and creates skill-specific slices so each specialist only receives the findings relevant to their expertise. This achieves approximately **74% token reduction** compared to sending all findings to every specialist, significantly reducing API costs and improving response quality.

## The 9 Specialist Skills

Each skill is defined as a YAML skill file in `config/skills/security/`. The triage step selects skills based on the codebase's language, framework, and dependencies. The `false-positive-filter` is always included.

| Skill | Emoji | Focus Area |
|-------|-------|------------|
| **Vulnerability Analyst** | 🔍 | OWASP Top 10 across all code. Lead role -- runs first. |
| **Auth Reviewer** | 🔐 | OAuth flows, JWT validation, session handling, password storage |
| **Injection Checker** | 💉 | SQL, command, LDAP, XPath, NoSQL, template injection |
| **Secrets Detector** | 🔑 | Hardcoded API keys, tokens, connection strings, credentials in source |
| **False Positive Filter** | 🧹 | Reviews all findings, removes confidence < 8 and invalid results |
| **Config Auditor** | ⚙️ | Security misconfigurations, debug settings, permissive CORS, missing headers |
| **Supply Chain Auditor** | 📦 | Dependency vulnerabilities, lockfile integrity, typosquatting |
| **Compliance Checker** | 📜 | PII handling, encryption requirements, regulatory compliance patterns |
| **AI Security Reviewer** | 🤖 | Prompt injection, unsafe model output handling, LLM-specific vulnerabilities |

### How Skills Interact

The skills run in a structured discussion pattern:

1. **Static analysis** (steps 4-6) produces raw findings from patterns, git history, and dependency audits
2. **Compression** (step 7) groups and slices findings for each specialist
3. **Triage** selects relevant skills based on the codebase and findings
4. **Round 1**: Each skill reviews the code and its finding slice
5. **ConvergenceCheck**: If any skill objects (e.g., the False Positive Filter disagrees with a finding), another round runs for objecting skills
6. **Convergence**: Once all skills agree, findings are consolidated and extracted

```
StaticPatternScan → 47 pattern matches across 6 categories
GitHistoryScan → 2 secrets found in history (CRITICAL)
DependencyAudit → 3 vulnerable packages, 1 missing lockfile
CompressSecurityFindings → grouped into skill-specific slices (74% token reduction)

SecurityTriage → "This is a .NET API with EF Core, JWT auth, and OpenAI integration"
  → vuln-analyst (round 1): 5 findings
  → auth-reviewer (round 1): 3 findings
  → injection-checker (round 1): 2 findings
  → secrets-detector (round 1): 3 findings (2 from history)
  → config-auditor (round 1): 2 findings
  → ai-security-reviewer (round 1): 1 finding
  → false-positive-filter (round 1): "OBJECTION — finding #4 is in test code"
ConvergenceCheck → unresolved objection → round 2
  → false-positive-filter (round 2): "Retained 14 of 16 findings"
ConvergenceCheck → consensus reached
CompileDiscussion → final report
ExtractFindings → 14 structured Finding records
DeliverFindings → console + SARIF output
```

### Customizing Skills

Each skill's behavior is controlled by its YAML skill file. For example, `config/skills/security/vuln-analyst.yaml`:

```yaml
name: vuln-analyst
display_name: "Vulnerability Analyst"
emoji: "🔍"
description: "Identifies high-confidence security vulnerabilities"

triggers:
  - security-scan
  - code-change
  - api-endpoint
  - user-input

rules: |
  You are a security vulnerability analyst...
  - Analyze every changed file for OWASP Top 10 vulnerabilities
  - Only report findings with confidence >= 8
  - For each finding: cite the specific code line and explain the attack vector

convergence_criteria:
  - "All changed files have been reviewed"
  - "No HIGH severity finding left unexamined"
```

You can modify triggers, rules, and convergence criteria to match your team's security standards.

## Output Formats

The `--output` flag controls how findings are delivered:

=== "Console (default)"

    Findings printed to stdout with severity coloring:

    ```
    [CRITICAL] AWS Access Key in git history — config/aws.json (commit a1b2c3d, 2025-11-03)
              Provider: AWS | Revoke: https://console.aws.amazon.com/iam/home#/security_credentials
    [HIGH] SQL Injection in UserRepository.cs:47
           String concatenation in WHERE clause with user-supplied email parameter
    [MEDIUM] Missing HttpOnly flag on auth cookie — AuthController.cs:23
    ```

=== "SARIF"

    Industry-standard Static Analysis Results Interchange Format. Import into GitHub Advanced Security, Azure DevOps, or any SARIF viewer:

    ```json
    {
      "$schema": "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/main/sarif-2.1/schema/sarif-schema-2.1.0.json",
      "version": "2.1.0",
      "runs": [{
        "tool": { "driver": { "name": "AgentSmith SecurityScan" } },
        "results": [
          {
            "ruleId": "VULN-001",
            "level": "error",
            "message": { "text": "SQL Injection in UserRepository.cs:47" },
            "locations": [{ "physicalLocation": { "artifactLocation": { "uri": "src/Repositories/UserRepository.cs" }, "region": { "startLine": 47 } } }]
          }
        ]
      }]
    }
    ```

=== "Markdown"

    Structured report written to the output directory:

    ```markdown
    # Security Scan Results
    **Date:** 2026-03-26
    **Participants:** Vulnerability Analyst, Auth Reviewer, Injection Checker, Secrets Detector, Config Auditor, AI Security Reviewer

    ## Executive Summary
    Retained 14 of 16 findings (2 filtered as false positives)
    Static patterns: 47 matches | Git history: 2 secrets | Dependencies: 3 vulnerable

    ## Findings
    ### [CRITICAL] AWS Access Key in Git History
    **Source:** GitHistoryScan | **Commit:** a1b2c3d (2025-11-03)
    **Provider:** AWS | **Revoke:** https://console.aws.amazon.com/iam/home#/security_credentials

    ### [HIGH] SQL Injection in UserRepository.cs
    **File:** src/Repositories/UserRepository.cs:47
    **Attack vector:** User-supplied email parameter concatenated into SQL WHERE clause...
    ```

## CLI Examples

```bash
# Scan a local repo, console output
agent-smith security-scan --repo .

# Scan with SARIF output for CI integration
agent-smith security-scan --repo . --output sarif --output-dir ./reports

# Scan a specific branch, markdown output
agent-smith security-scan --repo ./my-api --branch feature/auth --output markdown

# Scan only the diff of a pull request
agent-smith security-scan --repo ./my-project --pr 42 --output markdown

# Dry run — show the pipeline without executing
agent-smith security-scan --repo ./my-project --dry-run

# Combine output formats
agent-smith security-scan --repo ./my-project --output sarif,markdown,console --output-dir ./reports
```

!!! tip "CI/CD integration"
    Use `--output sarif` in your CI pipeline and upload the result to GitHub Advanced Security or Azure DevOps. The exit code is non-zero when HIGH or CRITICAL severity findings are present. See [GitHub Actions](../cicd/github-actions.md), [Azure DevOps](../cicd/azure-devops.md), and [GitLab CI](../cicd/gitlab-ci.md) for ready-to-use pipeline configurations.

## Exclusion Rules

The `security-principles.md` file (loaded by `LoadDomainRules`) controls what the False Positive Filter removes. Common exclusions:

- Test-only code paths
- Placeholder/example credentials
- DoS without demonstrated exploit path
- Path-only SSRF (host not user-controlled)
- Race conditions without reproducible evidence

Place `security-principles.md` in your repo's `config/skills/security/` directory to customize exclusions per project.

## DAST (OWASP ZAP)

When enabled, the pipeline includes a ZAP scan step that tests the running application for runtime vulnerabilities -- XSS, CSRF, auth bypass, header misconfiguration. ZAP runs as a Docker container using the same `docker cp` pattern as Nuclei.

Three scan types are available: `baseline` (~2 min, passive), `full-scan` (~10 min, active injection), and `api-scan` (~5 min, OpenAPI-aware). Two dedicated skills (`dast-analyst` and `dast-false-positive-filter`) process ZAP findings alongside static analysis results.

See [Security Scan Configuration](../configuration/security-scan.md#dast-owasp-zap) for setup.

## Auto-Fix

Critical and High findings can be automatically submitted as fix PRs. After the scan completes, findings are grouped by file and category, and separate fix jobs are spawned. Each fix job runs the fix-bug pipeline with a security-specific system prompt.

Auto-fix is opt-in (`auto_fix.enabled: false` by default) and supports confirmation via [Interactive Dialogue](../concepts/interactive-dialogue.md) before spawning fixes.

See [Security Scan Configuration](../configuration/security-scan.md#auto-fix) for setup.

## Trend Analysis

Git-based security trend analysis tracks findings over time without any external database. Each scan writes structured data to `result.md` frontmatter, and the `SecurityTrend` command reads SARIF snapshots from Git history to compute deltas.

Use `agent-smith security-trend --project my-api` to view the trend from the CLI.

See [Security Scan Configuration](../configuration/security-scan.md#trend-analysis) for setup.
