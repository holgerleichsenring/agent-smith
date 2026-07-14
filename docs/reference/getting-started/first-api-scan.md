# First API Scan

Scan a running API for security vulnerabilities in under 2 minutes.

## Prerequisites

- Agent Smith [installed](../../get-it-running/install.md)
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

The scan runs the structured **api-security** pipeline. Deterministic scanners go first — [Nuclei](https://github.com/projectdiscovery/nuclei) probes the running API and [Spectral](https://github.com/stoplightio/spectral) lints the OpenAPI spec with OWASP rules. The **api-security-master** then triages and analyzes those results with a read-only view of the source (when one is available), and the delivered findings are the master's curated set plus any uncovered High+ scanner facts. See [API Scan](../pipelines/api-scan.md) for the full pipeline.

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

## Code-Aware Scans (Optional)

For richer findings with file:line evidence, add a `source:` block to the project
config — api-scan will resolve it automatically (local path or remote clone), no
`--source-path` needed:

```yaml
projects:
  api-security:
    source:
      type: GitHub                    # GitHub | GitLab | AzureRepos | Local
      url: https://github.com/owner/repo
      auth: token                     # token resolved from GITHUB_TOKEN env / secret store
    # ...
```

A missing or unreachable source falls back to passive schema-only mode without failing.
The `--source-path <local>` CLI flag still works for ad-hoc local overrides during
iteration and wins over any configured source.

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
  --agent claude-parallel \
  --swagger https://your-api.com/swagger.json \
  --target https://your-api.com \
  --config .agentsmith/agentsmith.yml \
  --output console,markdown
```

API scans are project-less (p0281d): `--agent <name>` picks an agent from the config's `agents:` catalog directly — no `--project` needed. Add `--source-path .` to enable code-aware scanning against the local checkout.

## In CI/CD

See [CI/CD Integration](../cicd/index.md) for Azure DevOps, GitHub Actions, and GitLab examples.
