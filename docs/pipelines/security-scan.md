# Security Scan

The **security-scan** pipeline performs a multi-layered code security review combining static pattern matching, git history analysis, dependency auditing, and a multi-role AI specialist panel. It assembles 9 specialist skills and runs them through a **structured** (deterministic) execution graph -- no LLM triage, no convergence rounds. Skills are organized into stages (contributors, gate, executor) derived from skill metadata, and findings flow as typed JSON between stages. The pipeline delivers findings in SARIF, Markdown, or console format.

!!! info "Pipeline type: structured"
    Since Phase 64, security-scan uses the **structured** pipeline type. `SkillGraphBuilder` builds a deterministic execution graph from `runs_after`/`runs_before` declarations in skill metadata. There is no LLM-based triage and no convergence checking, resulting in approximately **80% token reduction** compared to the previous discussion-based approach.

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
| 12 | SecurityTriage | Builds deterministic skill graph via `SkillGraphBuilder` (no LLM) |
| 13 | SkillRounds | Runs skills in staged order: contributors (parallel) then gate then executor |
| 14 | CompileDiscussion | Consolidates all findings into a final report |
| 15 | ExtractFindings | Gate produces typed `List<Finding>` -- bypasses raw extraction |
| 16 | DeliverFindings | Writes output in the requested format(s) |
| 17 | SecuritySnapshotWrite | Persists SARIF snapshot for trend history |
| 18 | SpawnFix | Spawns fix jobs for Critical/High findings (skips if `auto_fix.enabled: false`) |

!!! info "Deterministic execution graph"
    Step 12 uses `SkillGraphBuilder` to build an execution graph from skill metadata (`runs_after`/`runs_before` declarations). Skills are topologically sorted into stages: **contributors** run in parallel with category-sliced findings, the **gate** (false-positive-filter) runs next and can veto findings, and the **executor** (chain-analyst) runs last. There is no LLM triage and no convergence checking. The gate produces typed `List<Finding>` output that flows directly to `DeliverFindings`, bypassing raw text extraction.

## Static Pattern Scan

The `StaticPatternScan` step runs 91 regex patterns organized into 6 categories. Patterns ship in the [agentsmith-skills](https://github.com/holgerleichsenring/agent-smith-skills) release tarball alongside skills, and are loaded from `{cacheDir}/patterns/*.yaml` after the catalog is pulled at boot:

| Category | Patterns | Examples |
|----------|----------|----------|
| **secrets** | 27 | AWS keys, GitHub tokens, private keys, connection strings |
| **injection** | 16 | SQL injection, command injection, XPath, template injection |
| **ssrf** | 12 | URL construction from user input, DNS rebinding vectors |
| **config** | 15 | Debug mode enabled, permissive CORS, missing security headers |
| **compliance** | 10 | PII logging, missing encryption, weak hashing algorithms |
| **ai-security** | 11 | Prompt injection, unsafe deserialization of model output, API key in prompts |

Pattern files are extensible -- contribute upstream via a PR against [agentsmith-skills](https://github.com/holgerleichsenring/agent-smith-skills), or override per-deployment via `AGENTSMITH_CONFIG_DIR`. See [Custom Security Patterns](../security/custom-patterns.md) for both paths.

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

Each skill is defined as a YAML skill file in `config/skills/security/`. The triage step selects skills based on the codebase's language, framework, and dependencies. The `false-positive-filter` is always included, and `chain-analyst` is the final executor.

p0094b reduced the set from 15 to 9 by removing overlapping attacker-perspective skills whose signals, in a code-audit context, duplicated the knowledge-domain skills. The attacker skills remain in `api-security` where HTTP probing and persona-based testing are distinct capabilities.

| Skill | Emoji | Focus Area |
|-------|-------|------------|
| **Auth Reviewer** | 🔐 | OAuth, JWT, session handling, password storage, **IDOR/BOLA** (sequential IDs, ownership checks, cross-tenant) |
| **Injection Checker** | 💉 | SQL, command, LDAP, XPath, NoSQL, template injection, SSRF |
| **Secrets Detector** | 🔑 | Hardcoded API keys, tokens, connection strings, credentials in source |
| **Config Auditor** | ⚙️ | Security misconfigurations, debug settings, permissive CORS, missing headers |
| **Supply Chain Auditor** | 📦 | Dependency vulnerabilities, lockfile integrity, typosquatting |
| **Compliance Checker** | 📜 | PII handling, encryption requirements, regulatory compliance patterns |
| **AI Security Reviewer** | 🤖 | Prompt injection, unsafe model output handling, LLM-specific vulnerabilities |
| **False Positive Filter** | 🧹 | Gate: reviews all findings, removes confidence < 8 and invalid results |
| **Chain Analyst** | 🔗 | Executor: synthesizes across commodity + skill findings, reasons about multi-step attack chains, deduplicates |

### How Skills Collaborate

Security scan uses the **structured pipeline** pattern. For a general overview of all pipeline orchestration patterns, see [Multi-Agent Orchestration](../concepts/multi-agent-orchestration.md).

Skills run in a **deterministic staged graph** built by `SkillGraphBuilder`:

1. **Static analysis** (steps 4-6) produces raw findings from patterns, git history, and dependency audits
2. **Compression** (step 9) groups and slices findings for each specialist
3. **Triage** builds a skill execution graph from `runs_after`/`runs_before` metadata (no LLM call)
4. **Stage 1 -- Contributors** (parallel): Each specialist reviews its category-sliced findings in a single call
5. **Stage 2 -- Gate**: The false-positive-filter reviews all contributor output, produces typed `List<Finding>`, and can veto findings
6. **Stage 3 -- Executor**: The chain-analyst receives the filtered findings plus the full commodity-tool output (StaticPatternScan, GitHistoryScan, DependencyAudit) and synthesizes the final assessment, reasoning about multi-step attack chains

Each skill runs exactly once. There are no convergence rounds and no re-runs.

```
StaticPatternScan → 47 pattern matches across 6 categories
GitHistoryScan → 2 secrets found in history (CRITICAL)
DependencyAudit → 3 vulnerable packages, 1 missing lockfile
CompressSecurityFindings → grouped into skill-specific slices (74% token reduction)

SecurityTriage → SkillGraphBuilder builds execution graph (deterministic, no LLM)
  Stage 1 (contributors, parallel):
    → auth-reviewer: 3 findings (typed JSON)
    → injection-checker: 2 findings (typed JSON)
    → secrets-detector: 3 findings (typed JSON)
    → config-auditor: 2 findings (typed JSON)
    → ai-security-reviewer: 1 finding (typed JSON)
  Stage 2 (gate):
    → false-positive-filter: vetoes 2 findings → typed List<Finding> (14 retained)
  Stage 3 (executor):
    → chain-analyst: synthesizes final assessment (with commodity findings + skill outputs)
DeliverFindings → console + SARIF output (typed findings, no raw extraction needed)
```

### Customizing Skills

Each skill's behavior is controlled by its SKILL.md + agentsmith.md pair. For example, `config/skills/security/auth-reviewer/`:

```markdown
---
name: auth-reviewer
description: "Specializes in authentication and authorization: OAuth, JWT, session handling, IDOR/BOLA"
---

# Auth Reviewer

You are a security specialist focused on authentication and authorization.
Your task:
- Check OAuth flows for CSRF protection (state parameter)
- Verify JWT validation: signature, expiry, issuer, audience
- Check for IDOR/BOLA: sequential IDs in paths, missing ownership predicates,
  cross-tenant access, bulk operations bypassing per-item authorization
- ...
```

The `agentsmith.md` file holds orchestration metadata (role, output type, runs_after/runs_before declarations, input_categories). Gate-role skills with `output: list` must declare `input_categories` explicitly — `*` for all categories or a comma-separated list.

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
