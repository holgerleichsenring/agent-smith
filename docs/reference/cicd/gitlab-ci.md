# GitLab CI/CD

The examples assume the repository carries `ci/agentsmith.yml` with an agent named `ci-scan`, as described on the [CI/CD overview](index.md#what-a-ci-run-needs).

## Security scan (code analysis)

Run the security-scan pipeline with static pattern matching, git history scanning, dependency auditing and the security master's review. The SARIF file is published as a SAST report.

```yaml
# .gitlab-ci.yml
stages:
  - security

security-scan:
  stage: security
  image: debian:bookworm-slim
  variables:
    GIT_DEPTH: 500  # history for the git history scan
  before_script:
    - apt-get update -qq && apt-get install -y -qq curl ca-certificates git > /dev/null
    - curl -fsSL -o /usr/local/bin/agent-smith
        https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
    - chmod +x /usr/local/bin/agent-smith
  script:
    - agent-smith security-scan
        --config ci/agentsmith.yml
        --agent ci-scan
        --source-path $CI_PROJECT_DIR
        --output console,sarif,markdown
        --output-dir ./results
  artifacts:
    paths:
      - results/
    reports:
      sast:
        - results/findings.sarif
    when: always
    expire_in: 30 days
  rules:
    - if: $CI_PIPELINE_SOURCE == "merge_request_event"
    - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
```

`ANTHROPIC_API_KEY` comes from the project's CI/CD variables; a job sees them without being listed.

!!! tip "Git history scanning"
    Set `GIT_DEPTH` so the `GitHistoryScan` step has commits to read. It scans the last 500. GitLab's default shallow clone may not include enough history.

!!! info "Dependency audit"
    The dependency audit runs the ecosystem's own tool (`npm audit`, `pip-audit`, `dotnet list package --vulnerable`) on the runner, because the CLI runs its sandbox in-process. Use a job image that has the toolchain of the scanned repository.

## API scan

`api-scan` probes a running API, so it needs the OpenAPI description (`--swagger`, a path or URL) and the base URL (`--target`). Point it at a test or staging deployment. Without a Docker socket in the job, Nuclei, Spectral and ZAP run as local processes and must be on `PATH`; see [Tool configuration](../configuration/tools.md) and the Docker variant below.

```yaml
api-scan:
  stage: security
  image: debian:bookworm-slim
  before_script:
    - apt-get update -qq && apt-get install -y -qq curl ca-certificates > /dev/null
    - curl -fsSL -o /usr/local/bin/agent-smith
        https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
    - chmod +x /usr/local/bin/agent-smith
  script:
    - agent-smith api-scan
        --config ci/agentsmith.yml
        --agent ci-scan
        --swagger https://api.staging.example.com/swagger/v1/swagger.json
        --target https://api.staging.example.com
        --output console,sarif,markdown
        --output-dir ./results
  artifacts:
    paths:
      - results/
    reports:
      sast:
        - results/findings.sarif
    when: always
```

!!! info "SARIF as SAST report"
    GitLab reads the file under `reports:sast` and shows the findings in its security views, where your GitLab tier supports them.

## Docker variant

With Docker-in-Docker available, run the CLI image and let the scanner tools start their own containers:

```yaml
api-scan-docker:
  stage: security
  image: docker:27
  services:
    - docker:27-dind
  variables:
    DOCKER_TLS_CERTDIR: "/certs"
  script:
    - docker run --rm
        -e ANTHROPIC_API_KEY
        -v $CI_PROJECT_DIR:/repo
        -v $CI_PROJECT_DIR/results:/output
        -v /var/run/docker.sock:/var/run/docker.sock
        holgerleichsenring/agent-smith-cli:latest
        api-scan --config /repo/ci/agentsmith.yml --agent ci-scan
          --swagger https://api.staging.example.com/swagger/v1/swagger.json
          --target https://api.staging.example.com
          --output console,sarif --output-dir /output
  artifacts:
    paths:
      - results/
    reports:
      sast:
        - results/findings.sarif
    when: always
```

## ARM64 runners

For ARM64 GitLab runners, download the ARM64 binary:

```yaml
security-scan:
  tags:
    - arm64
  before_script:
    - curl -fsSL -o /usr/local/bin/agent-smith
        https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-arm64
    - chmod +x /usr/local/bin/agent-smith
```

## Quality gate

Fail the pipeline when Critical or High findings are present. They carry the SARIF level `error`:

```yaml
check-findings:
  stage: security
  needs: [security-scan]
  image: debian:bookworm-slim
  before_script:
    - apt-get update -qq && apt-get install -y -qq jq > /dev/null
  script:
    - |
      if [ -f results/findings.sarif ]; then
        CRITICAL=$(jq '[.runs[].results[] | select(.level == "error")] | length' results/findings.sarif)
        echo "Critical or high findings: $CRITICAL"
        if [ "$CRITICAL" -gt 0 ]; then
          echo "ERROR: $CRITICAL critical or high security findings detected"
          exit 1
        fi
      fi
```

## Variables setup

Add this in **Settings > CI/CD > Variables** and mask it:

| Variable            | Required | Description                                |
|---------------------|----------|--------------------------------------------|
| `ANTHROPIC_API_KEY` | Yes      | Key for the `claude` agent; use your provider's variable for another agent type |
