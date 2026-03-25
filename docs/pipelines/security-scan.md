# Security Scan

The **security-scan** pipeline performs a multi-role AI code review for security vulnerabilities. It assembles a panel of specialist roles, runs a structured discussion with convergence checking, and delivers findings in SARIF, Markdown, or console format.

## Pipeline Steps

| # | Command | What It Does |
|---|---------|-------------|
| 1 | CheckoutSource | Clones repo, optionally scopes to a PR diff or branch |
| 2 | BootstrapProject | Detects language, framework, dependencies |
| 3 | LoadDomainRules | Loads `security-principles.md` with exclusion rules |
| 4 | AnalyzeCode | Scout agent maps file structure and dependency graph |
| 5 | SecurityTriage | AI selects which specialist roles should participate |
| 6 | ConvergenceCheck | Evaluates if all roles agree; re-runs objecting roles if not |
| 7 | CompileDiscussion | Consolidates all findings into a final report |
| 8 | DeliverOutput | Writes output in the requested format(s) |

!!! info "Dynamic expansion"
    Steps 5-6 are dynamic. `SecurityTriage` inserts `SecuritySkillRound` commands for each selected role, plus a `ConvergenceCheck`. If roles disagree, `ConvergenceCheck` inserts additional rounds until consensus or the max round limit is reached (default: 3).

## The 5 Specialist Roles

Each role is defined as a YAML skill file in `config/skills/security/`. The triage step selects roles based on the codebase's language, framework, and dependencies.

| Role | Emoji | Focus Area |
|------|-------|------------|
| **Vulnerability Analyst** | :mag: | OWASP Top 10 across all code. Lead role — runs first. |
| **Auth Reviewer** | :locked_with_key: | OAuth flows, JWT validation, session handling, password storage |
| **Injection Checker** | :syringe: | SQL, command, LDAP, XPath, NoSQL, template injection |
| **Secrets Detector** | :key: | Hardcoded API keys, tokens, connection strings, credentials in source |
| **False Positive Filter** | :broom: | Reviews all findings, removes confidence < 8 and invalid results |

### How Roles Interact

The roles run in a structured discussion pattern:

1. **Triage** selects relevant roles based on the codebase analysis
2. **Round 1**: Each role reviews the code and produces findings
3. **ConvergenceCheck**: If any role objects (e.g., the False Positive Filter disagrees with a finding), another round runs for objecting roles
4. **Convergence**: Once all roles agree, findings are consolidated

```
SecurityTriage → "This is a .NET API with EF Core and JWT auth"
  → vuln-analyst (round 1): 5 findings
  → auth-reviewer (round 1): 3 findings
  → injection-checker (round 1): 2 findings
  → secrets-detector (round 1): 1 finding
  → false-positive-filter (round 1): "OBJECTION — finding #4 is in test code"
ConvergenceCheck → unresolved objection → round 2
  → false-positive-filter (round 2): "Retained 8 of 11 findings"
ConvergenceCheck → consensus reached
CompileDiscussion → final report
```

### Customizing Roles

Each role's behavior is controlled by its YAML skill file. For example, `config/skills/security/vuln-analyst.yaml`:

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
    **Date:** 2026-03-25
    **Participants:** Vulnerability Analyst, Auth Reviewer, Injection Checker

    ## Executive Summary
    Retained 8 of 11 findings (3 filtered as false positives)

    ## Findings
    ### [HIGH] SQL Injection in UserRepository.cs
    **File:** src/Repositories/UserRepository.cs:47
    **Attack vector:** User-supplied email parameter concatenated into SQL WHERE clause...
    ```

## CLI Examples

```bash
# Scan a local repo, console output
agent-smith security-scan --repo ./my-project

# Scan a specific branch, SARIF output
agent-smith security-scan --repo ./my-project --branch feature/auth --output sarif

# Scan only the diff of a pull request
agent-smith security-scan --repo ./my-project --pr 42 --output markdown

# Dry run — show the pipeline without executing
agent-smith security-scan --repo ./my-project --dry-run

# Combine output formats
agent-smith security-scan --repo ./my-project --output sarif,markdown,console
```

!!! tip "CI/CD integration"
    Use `--output sarif` in your CI pipeline and upload the result to GitHub Advanced Security or Azure DevOps. The exit code is non-zero when HIGH severity findings are present.

## Exclusion Rules

The `security-principles.md` file (loaded by `LoadDomainRules`) controls what the False Positive Filter removes. Common exclusions:

- Test-only code paths
- Placeholder/example credentials
- DoS without demonstrated exploit path
- Path-only SSRF (host not user-controlled)
- Race conditions without reproducible evidence

Place `security-principles.md` in your repo's `config/skills/security/` directory to customize exclusions per project.
