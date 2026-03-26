# GitHub Actions

## Binary Download + API Scan

Download the binary, run a scan, and upload SARIF results to the GitHub Security tab.

```yaml
# .github/workflows/security-scan.yml
name: Agent Smith Security Scan

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

permissions:
  security-events: write  # Required for SARIF upload
  contents: read

jobs:
  api-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Download Agent Smith
        run: |
          curl -fsSL -o agent-smith \
            https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
          chmod +x agent-smith

      - name: Run API Security Scan
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
        run: |
          ./agent-smith api-scan \
            --repo ${{ github.workspace }} \
            --output console,sarif,summary \
            --output-dir ./results

      - name: Upload SARIF to GitHub Security
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: ./results/results.sarif
          category: agent-smith-api-scan

      - name: Upload Report Artifact
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: security-report
          path: ./results/
```

!!! tip "GitHub Security Tab"
    The `github/codeql-action/upload-sarif@v3` action uploads findings to the **Security** tab of your repository. Findings appear alongside CodeQL results, with full code location links and severity levels.

## Security Scan with SARIF Upload

Run the full security-scan pipeline (static patterns, git history, dependency audit, AI specialist panel) and upload SARIF results to the GitHub Security tab.

```yaml
# .github/workflows/security-scan.yml
name: Agent Smith Code Security Scan

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

permissions:
  security-events: write
  contents: read

jobs:
  security-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 500  # Required for git history scanning

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
            --repo . \
            --output sarif \
            --output-dir ./security-results

      - name: Upload SARIF
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: ./security-results/findings.sarif
          category: agent-smith-security-scan

      - name: Upload Report Artifact
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: security-scan-report
          path: ./security-results/
```

!!! tip "Git history scanning"
    Set `fetch-depth: 500` on the checkout step so the `GitHistoryScan` step can scan commit history for leaked secrets. Without sufficient history, only the current tree is scanned.

## PR Comment with Findings

Post a Markdown summary as a PR comment:

```yaml
      - name: Comment on PR
        if: github.event_name == 'pull_request' && always()
        uses: marocchino/sticky-pull-request-comment@v2
        with:
          path: ./results/summary.md
          header: agent-smith-scan
```

## Self-Hosted Runners (ARM64)

For ARM64 runners (e.g., Graviton):

```yaml
      - name: Download Agent Smith (ARM64)
        run: |
          curl -fsSL -o agent-smith \
            https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-arm64
          chmod +x agent-smith
```

## macOS Runners

```yaml
  api-scan-macos:
    runs-on: macos-latest
    steps:
      - uses: actions/checkout@v4
      - name: Download Agent Smith
        run: |
          curl -fsSL -o agent-smith \
            https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-osx-arm64
          chmod +x agent-smith
```

## Quality Gate

Fail the workflow when findings exceed a threshold:

```yaml
      - name: Check Findings
        if: always()
        run: |
          if [ -f ./results/results.sarif ]; then
            ERRORS=$(jq '[.runs[].results[] | select(.level == "error")] | length' ./results/results.sarif)
            echo "Critical findings: $ERRORS"
            if [ "$ERRORS" -gt 0 ]; then
              echo "::error::Found $ERRORS critical security findings"
              exit 1
            fi
          fi
```

## Secrets Configuration

Add these in **Settings > Secrets and variables > Actions**:

| Secret              | Required | Description              |
|---------------------|----------|--------------------------|
| `ANTHROPIC_API_KEY` | Yes      | Claude API key           |
| `GITHUB_TOKEN`      | Auto     | Provided by Actions runtime |
