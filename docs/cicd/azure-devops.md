# Azure DevOps Pipelines

## Binary Approach

Download the binary, run a scan, and publish results as a pipeline summary tab and artifact.

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include: [main]

pool:
  vmImage: ubuntu-latest

steps:
  - task: Bash@3
    displayName: Download Agent Smith
    inputs:
      targetType: inline
      script: |
        curl -fsSL -o $(Agent.TempDirectory)/agent-smith \
          https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
        chmod +x $(Agent.TempDirectory)/agent-smith

  - task: Bash@3
    displayName: Run API Security Scan
    env:
      ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)
    inputs:
      targetType: inline
      script: |
        $(Agent.TempDirectory)/agent-smith api-scan \
          --repo $(Build.SourcesDirectory) \
          --output console,summary,sarif \
          --output-dir $(Build.ArtifactStagingDirectory)/security

  - task: Bash@3
    displayName: Publish Summary Tab
    condition: always()
    inputs:
      targetType: inline
      script: |
        SUMMARY=$(Build.ArtifactStagingDirectory)/security/summary.md
        if [ -f "$SUMMARY" ]; then
          echo "##vso[task.uploadsummary]$SUMMARY"
        fi

  - task: PublishBuildArtifacts@1
    displayName: Publish Security Report
    condition: always()
    inputs:
      pathToPublish: $(Build.ArtifactStagingDirectory)/security
      artifactName: security-report
```

!!! info "Summary Tab"
    The `##vso[task.uploadsummary]` command attaches a Markdown file as a tab on the pipeline run page. Team members see the findings without digging into logs.

## Docker Compose Variant

For teams that prefer Docker or need tool containers (Nuclei, Spectral):

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include: [main]

pool:
  vmImage: ubuntu-latest

steps:
  - task: Bash@3
    displayName: Run Agent Smith (Docker)
    env:
      ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)
    inputs:
      targetType: inline
      script: |
        docker run --rm \
          -e ANTHROPIC_API_KEY=$ANTHROPIC_API_KEY \
          -v $(Build.SourcesDirectory):/app/repo \
          -v /var/run/docker.sock:/var/run/docker.sock \
          -v $(Build.ArtifactStagingDirectory)/security:/app/output \
          holgerleichsenring/agent-smith:latest \
          api-scan --repo /app/repo --output console,summary,sarif --output-dir /app/output

  - task: Bash@3
    displayName: Publish Summary Tab
    condition: always()
    inputs:
      targetType: inline
      script: |
        SUMMARY=$(Build.ArtifactStagingDirectory)/security/summary.md
        if [ -f "$SUMMARY" ]; then
          echo "##vso[task.uploadsummary]$SUMMARY"
        fi

  - task: PublishBuildArtifacts@1
    displayName: Publish Security Report
    condition: always()
    inputs:
      pathToPublish: $(Build.ArtifactStagingDirectory)/security
      artifactName: security-report
```

## Security Scan (Code Analysis)

Run the security-scan pipeline for static analysis, git history scanning, dependency auditing, and AI-powered code review. Results are published as SARIF artifacts.

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include: [main]

pool:
  vmImage: ubuntu-latest

steps:
  - task: Bash@3
    displayName: Download Agent Smith
    inputs:
      targetType: inline
      script: |
        curl -fsSL -o $(Agent.TempDirectory)/agent-smith \
          https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
        chmod +x $(Agent.TempDirectory)/agent-smith

  - task: Bash@3
    displayName: Run agent-smith security scan
    env:
      ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)
    inputs:
      targetType: inline
      script: |
        $(Agent.TempDirectory)/agent-smith security-scan \
          --repo $(Build.SourcesDirectory) \
          --output console,sarif \
          --output-dir $(Build.ArtifactStagingDirectory)/security

  - task: Bash@3
    displayName: Publish Summary Tab
    condition: always()
    inputs:
      targetType: inline
      script: |
        SUMMARY=$(Build.ArtifactStagingDirectory)/security/summary.md
        if [ -f "$SUMMARY" ]; then
          echo "##vso[task.uploadsummary]$SUMMARY"
        fi

  - task: PublishBuildArtifacts@1
    displayName: Publish Security Report
    condition: always()
    inputs:
      pathToPublish: $(Build.ArtifactStagingDirectory)/security
      artifactName: security-scan-report
```

!!! tip "Security scan vs API scan"
    The `security-scan` pipeline analyzes source code with 91 static patterns, git history scanning, and dependency auditing before the AI panel reviews findings. Use `api-scan` for runtime API endpoint testing with Nuclei and Spectral.

## Security Scan with Gate

Fail the pipeline when critical findings are detected:

```yaml
  - task: Bash@3
    displayName: Check Findings
    inputs:
      targetType: inline
      script: |
        SARIF=$(Build.ArtifactStagingDirectory)/security/results.sarif
        if [ -f "$SARIF" ]; then
          CRITICAL=$(jq '[.runs[].results[] | select(.level == "error")] | length' "$SARIF")
          if [ "$CRITICAL" -gt 0 ]; then
            echo "##vso[task.logissue type=error]Found $CRITICAL critical security findings"
            exit 1
          fi
        fi
```

## Variables Setup

Add these as pipeline variables (mark as secret):

| Variable            | Required | Description              |
|--------------------|----------|--------------------------|
| `ANTHROPIC_API_KEY`| Yes      | Claude API key for analysis |
| `GITHUB_TOKEN`     | No       | For GitHub source repos  |
| `AZURE_DEVOPS_TOKEN`| No     | For Azure Repos          |
