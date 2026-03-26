# GitLab CI/CD

## Binary Download + API Scan

```yaml
# .gitlab-ci.yml
stages:
  - security

api-scan:
  stage: security
  image: debian:bookworm-slim
  variables:
    ANTHROPIC_API_KEY: $ANTHROPIC_API_KEY
  before_script:
    - apt-get update -qq && apt-get install -y -qq curl jq > /dev/null
    - curl -fsSL -o /usr/local/bin/agent-smith
        https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
    - chmod +x /usr/local/bin/agent-smith
  script:
    - agent-smith api-scan
        --repo $CI_PROJECT_DIR
        --output console,sarif,summary,markdown
        --output-dir ./results
  artifacts:
    paths:
      - results/
    reports:
      sast:
        - results/results.sarif
    when: always
    expire_in: 30 days
  rules:
    - if: $CI_PIPELINE_SOURCE == "merge_request_event"
    - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
```

!!! info "SARIF as SAST Report"
    GitLab recognizes SARIF files under `reports:sast`. Findings appear in the **Security** dashboard and as inline annotations on merge requests.

## Security Scan (Code Analysis)

Run the full security-scan pipeline with static pattern matching, git history scanning, dependency auditing, and AI specialist panel. Results are published as SAST reports in the GitLab Security dashboard.

```yaml
security-scan:
  stage: security
  image: debian:bookworm-slim
  variables:
    ANTHROPIC_API_KEY: $ANTHROPIC_API_KEY
    GIT_DEPTH: 500  # Required for git history scanning
  before_script:
    - apt-get update -qq && apt-get install -y -qq curl > /dev/null
    - curl -fsSL -o /usr/local/bin/agent-smith
        https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
    - chmod +x /usr/local/bin/agent-smith
  script:
    - agent-smith security-scan
        --repo $CI_PROJECT_DIR
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

!!! tip "Git history scanning"
    Set `GIT_DEPTH: 500` so the `GitHistoryScan` step can scan commit history for leaked secrets. The default shallow clone depth in GitLab CI may not include enough history.

## ARM64 Runners

For ARM64 GitLab runners (e.g., AWS Graviton):

```yaml
api-scan:
  tags:
    - arm64
  before_script:
    - curl -fsSL -o /usr/local/bin/agent-smith
        https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-arm64
    - chmod +x /usr/local/bin/agent-smith
```

## Quality Gate

Fail the pipeline on critical findings:

```yaml
check-findings:
  stage: security
  needs: [api-scan]
  image: debian:bookworm-slim
  before_script:
    - apt-get update -qq && apt-get install -y -qq jq > /dev/null
  script:
    - |
      if [ -f results/results.sarif ]; then
        CRITICAL=$(jq '[.runs[].results[] | select(.level == "error")] | length' results/results.sarif)
        echo "Critical findings: $CRITICAL"
        if [ "$CRITICAL" -gt 0 ]; then
          echo "ERROR: $CRITICAL critical security findings detected"
          exit 1
        fi
      fi
  artifacts:
    paths:
      - results/
```

## Docker Variant

When you need tool containers (Nuclei, Spectral) and have Docker-in-Docker available:

```yaml
api-scan-docker:
  stage: security
  image: docker:27
  services:
    - docker:27-dind
  variables:
    DOCKER_TLS_CERTDIR: "/certs"
    ANTHROPIC_API_KEY: $ANTHROPIC_API_KEY
  script:
    - docker run --rm
        -e ANTHROPIC_API_KEY=$ANTHROPIC_API_KEY
        -v $CI_PROJECT_DIR:/app/repo
        -v /var/run/docker.sock:/var/run/docker.sock
        holgerleichsenring/agent-smith:latest
        api-scan --repo /app/repo --output console,sarif --output-dir /app/repo/results
  artifacts:
    paths:
      - results/
    reports:
      sast:
        - results/results.sarif
    when: always
```

## Variables Setup

Add these in **Settings > CI/CD > Variables** (mask and protect them):

| Variable            | Required | Description              |
|--------------------|----------|--------------------------|
| `ANTHROPIC_API_KEY`| Yes      | Claude API key           |
| `GITLAB_TOKEN`     | No       | For cross-project access |
