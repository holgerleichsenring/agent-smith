# GitHub Actions

The examples assume the repository carries `ci/agentsmith.yml` with an agent named `ci-scan`, as described on the [CI/CD overview](index.md#what-a-ci-run-needs).

## Security scan with SARIF upload

Run the security-scan pipeline (static patterns, git history, dependency audit, the security master's review) and upload the SARIF results to the repository's Security tab.

```yaml
# .github/workflows/security-scan.yml
name: Agent Smith Security Scan

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

permissions:
  security-events: write  # required for SARIF upload
  contents: read

jobs:
  security-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 500  # history for the git history scan

      - name: Download Agent Smith
        run: |
          curl -fsSL -o agent-smith \
            https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
          chmod +x agent-smith

      - name: Run security scan
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
        run: |
          ./agent-smith security-scan \
            --config ci/agentsmith.yml \
            --agent ci-scan \
            --source-path . \
            --output console,sarif,markdown \
            --output-dir ./results

      - name: Upload SARIF
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: ./results/findings.sarif
          category: agent-smith-security-scan

      - name: Upload report artifact
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: security-scan-report
          path: ./results/
```

!!! tip "GitHub Security tab"
    `github/codeql-action/upload-sarif` puts the findings in the **Security** tab of your repository, next to CodeQL results, with code locations and severity levels.

!!! tip "Git history scanning"
    Set `fetch-depth` on the checkout step so the `GitHistoryScan` step has commits to read. It scans the last 500. With the default shallow clone only the current commit is available.

## API scan

`api-scan` probes a running API, so it needs the OpenAPI description (`--swagger`, a path or URL) and the base URL (`--target`). Point it at a test or staging deployment. GitHub-hosted Ubuntu runners have Docker, so Nuclei, Spectral and ZAP run in containers.

```yaml
  api-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Download Agent Smith
        run: |
          curl -fsSL -o agent-smith \
            https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
          chmod +x agent-smith

      - name: Run API security scan
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
        run: |
          ./agent-smith api-scan \
            --config ci/agentsmith.yml \
            --agent ci-scan \
            --swagger https://api.staging.example.com/swagger/v1/swagger.json \
            --target https://api.staging.example.com \
            --output console,sarif,markdown \
            --output-dir ./api-results

      - name: Upload SARIF
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: ./api-results/findings.sarif
          category: agent-smith-api-scan
```

## PR comment with findings

Post the Markdown report as a sticky PR comment:

```yaml
      - name: Comment on PR
        if: github.event_name == 'pull_request' && always()
        uses: marocchino/sticky-pull-request-comment@v2
        with:
          path: ./results/findings.md
          header: agent-smith-scan
```

## Other runners

For ARM64 runners, download `agent-smith-linux-arm64`. On macOS runners, use `agent-smith-osx-arm64` (Apple silicon) or `agent-smith-osx-x64`:

```yaml
      - name: Download Agent Smith (ARM64)
        run: |
          curl -fsSL -o agent-smith \
            https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-arm64
          chmod +x agent-smith
```

## Quality gate

Fail the workflow when Critical or High findings are present. They carry the SARIF level `error`:

```yaml
      - name: Check findings
        if: always()
        run: |
          if [ -f ./results/findings.sarif ]; then
            ERRORS=$(jq '[.runs[].results[] | select(.level == "error")] | length' ./results/findings.sarif)
            echo "Critical or high findings: $ERRORS"
            if [ "$ERRORS" -gt 0 ]; then
              echo "::error::Found $ERRORS critical or high security findings"
              exit 1
            fi
          fi
```

## Secrets configuration

Add this in **Settings > Secrets and variables > Actions**:

| Secret              | Required | Description                                |
|---------------------|----------|--------------------------------------------|
| `ANTHROPIC_API_KEY` | Yes      | Key for the `claude` agent; use your provider's variable for another agent type |
