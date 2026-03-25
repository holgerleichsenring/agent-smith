# First API Scan

Scan a running API for security vulnerabilities in under 2 minutes.

## Prerequisites

- Agent Smith [installed](installation.md)
- `ANTHROPIC_API_KEY` (or other AI provider key)
- Docker running (for Nuclei and Spectral tool containers)
- A target API with a swagger.json endpoint

## Quick Run

```bash
export ANTHROPIC_API_KEY=sk-ant-...

agent-smith api-scan \
  --swagger https://your-api.com/swagger/v1/swagger.json \
  --target https://your-api.com \
  --output console
```

That's it. No config file needed for a basic scan.

## What Happens

The `api-scan` pipeline runs 8-11 steps:

1. **LoadSwagger** — fetches and parses the OpenAPI spec
2. **SpawnNuclei** — runs [Nuclei](https://github.com/projectdiscovery/nuclei) vulnerability scanner in a Docker container
3. **SpawnSpectral** — runs [Spectral](https://github.com/stoplightio/spectral) OpenAPI linter with OWASP rules
4. **LoadSkills** — loads API security specialist roles
5. **ApiSecurityTriage** — selects relevant specialists based on findings
6. **SkillRounds** — each specialist analyzes the results (1-3 rounds)
7. **ConvergenceCheck** — specialists reach consensus
8. **CompileFindings** — consolidates all findings
9. **DeliverFindings** — outputs results in your chosen format

## Output Formats

```bash
# Console output (default)
agent-smith api-scan --swagger ./spec.json --target https://api --output console

# Markdown report
agent-smith api-scan --swagger ./spec.json --target https://api --output markdown --output-dir ./reports

# SARIF for GitHub Security tab
agent-smith api-scan --swagger ./spec.json --target https://api --output sarif --output-dir ./reports

# Multiple formats at once
agent-smith api-scan --swagger ./spec.json --target https://api --output console,markdown,sarif --output-dir ./reports
```

## With Custom Configuration

For recurring scans with custom skills and tool config, create an `.agentsmith/` directory:

```
.agentsmith/
├── agentsmith.yml
├── nuclei.yaml          # custom Nuclei templates
├── spectral.yaml        # custom Spectral rules
└── skills/api-security/
    ├── api-design-auditor.yaml
    ├── auth-tester.yaml
    ├── api-vuln-analyst.yaml
    └── false-positive-filter.yaml
```

```bash
agent-smith api-scan \
  --swagger https://your-api.com/swagger.json \
  --target https://your-api.com \
  --config .agentsmith/agentsmith.yml \
  --project your-project-name \
  --output console,markdown
```

## In CI/CD

See [CI/CD Integration](../cicd/index.md) for Azure DevOps, GitHub Actions, and GitLab examples.
