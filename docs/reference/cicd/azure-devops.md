# Azure DevOps Pipelines

The examples assume the repository carries `ci/agentsmith.yml` with an agent named `ci-scan`, as described on the [CI/CD overview](index.md#what-a-ci-run-needs).

## Security scan (code analysis)

Run the security-scan pipeline for static patterns, git history scanning, dependency auditing and the security master's review of the source. The Markdown report becomes a tab on the run page, and the whole output directory is published as an artifact.

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include: [main]

pool:
  vmImage: ubuntu-latest

steps:
  - checkout: self
    fetchDepth: 500   # history for the git history scan

  - task: Bash@3
    displayName: Download Agent Smith
    inputs:
      targetType: inline
      script: |
        curl -fsSL -o $(Agent.TempDirectory)/agent-smith \
          https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
        chmod +x $(Agent.TempDirectory)/agent-smith

  - task: Bash@3
    displayName: Run security scan
    env:
      ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)
    inputs:
      targetType: inline
      script: |
        $(Agent.TempDirectory)/agent-smith security-scan \
          --config ci/agentsmith.yml \
          --agent ci-scan \
          --source-path $(Build.SourcesDirectory) \
          --output console,sarif,markdown \
          --output-dir $(Build.ArtifactStagingDirectory)/security

  - task: Bash@3
    displayName: Publish summary tab
    condition: always()
    inputs:
      targetType: inline
      script: |
        REPORT=$(Build.ArtifactStagingDirectory)/security/findings.md
        if [ -f "$REPORT" ]; then
          echo "##vso[task.uploadsummary]$REPORT"
        fi

  - task: PublishBuildArtifacts@1
    displayName: Publish security report
    condition: always()
    inputs:
      pathToPublish: $(Build.ArtifactStagingDirectory)/security
      artifactName: security-scan-report
```

!!! info "Summary tab"
    The `##vso[task.uploadsummary]` command attaches a Markdown file as a tab on the pipeline run page. Team members see the findings without digging into logs.

!!! tip "Security scan vs API scan"
    `security-scan` analyzes source code. Use `api-scan` for runtime testing of a deployed API with Nuclei, Spectral and ZAP.

## API scan

`api-scan` probes a running API, so it needs the OpenAPI description (`--swagger`, a path or URL) and the base URL (`--target`). Point it at a test or staging deployment.

```yaml
  - task: Bash@3
    displayName: Run API security scan
    env:
      ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)
    inputs:
      targetType: inline
      script: |
        $(Agent.TempDirectory)/agent-smith api-scan \
          --config ci/agentsmith.yml \
          --agent ci-scan \
          --swagger https://api.staging.example.com/swagger/v1/swagger.json \
          --target https://api.staging.example.com \
          --output console,sarif,markdown \
          --output-dir $(Build.ArtifactStagingDirectory)/api-security
```

Add `--source-path $(Build.SourcesDirectory)` when the API's source is checked out, so the master can anchor a finding to a file and line. The Microsoft-hosted `ubuntu-latest` image has Docker, so the scanner tools run in containers. On a self-hosted agent without Docker they run as local processes and must be on `PATH`, see [Tool configuration](../configuration/tools.md).

## Docker variant

For agents where you would rather run the CLI image than download the binary:

```yaml
  - task: Bash@3
    displayName: Run Agent Smith (Docker)
    env:
      ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)
    inputs:
      targetType: inline
      script: |
        docker run --rm \
          -e ANTHROPIC_API_KEY \
          -v $(Build.SourcesDirectory):/repo \
          -v $(Build.ArtifactStagingDirectory)/security:/output \
          holgerleichsenring/agent-smith-cli:latest \
          security-scan --config /repo/ci/agentsmith.yml --agent ci-scan \
            --source-path /repo --output console,sarif,markdown --output-dir /output
```

Mount `/var/run/docker.sock` as well when you run `api-scan` this way, so the scanner tools can start their containers.

## Security gate

Fail the pipeline when Critical or High findings are present. They carry the SARIF level `error`:

```yaml
  - task: Bash@3
    displayName: Check findings
    inputs:
      targetType: inline
      script: |
        SARIF=$(Build.ArtifactStagingDirectory)/security/findings.sarif
        if [ -f "$SARIF" ]; then
          CRITICAL=$(jq '[.runs[].results[] | select(.level == "error")] | length' "$SARIF")
          if [ "$CRITICAL" -gt 0 ]; then
            echo "##vso[task.logissue type=error]Found $CRITICAL critical or high security findings"
            exit 1
          fi
        fi
```

## Variables setup

Add these as pipeline variables and mark them secret:

| Variable            | Required | Description                                |
|---------------------|----------|--------------------------------------------|
| `ANTHROPIC_API_KEY` | Yes      | Key for the `claude` agent; use your provider's variable for another agent type |
