# CI/CD Integration

Agent Smith can run directly inside your CI/CD pipeline to perform security scans, API audits, and code analysis on every build. There are two approaches depending on your environment.

## Binary Approach (Recommended)

Download the self-contained binary for your runner's platform. No Docker, no .NET runtime, no dependencies.

```bash
# Download (one line)
curl -fsSL -o agent-smith \
  https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64

chmod +x agent-smith

# Run a security scan
./agent-smith security-scan \
  --repo . \
  --output console,sarif \
  --output-dir ./results
```

Available platforms:

| Platform         | Binary name               |
|------------------|---------------------------|
| Linux x64        | `agent-smith-linux-x64`   |
| Linux ARM64      | `agent-smith-linux-arm64` |
| macOS x64        | `agent-smith-osx-x64`     |
| macOS ARM64      | `agent-smith-osx-arm64`   |
| Windows x64      | `agent-smith-win-x64`     |

!!! tip "Why binary over Docker?"
    The binary approach is faster (no image pull), simpler (no Docker-in-Docker), and works on any runner that can execute a native binary. Choose Docker Compose only when you need the full stack (dispatcher, Redis, webhooks).

## Docker Compose Approach

Use when you need the full Agent Smith stack, including tool containers (Nuclei, Spectral) or the dispatcher.

```yaml
# docker-compose.ci.yml
services:
  agentsmith:
    image: holgerleichsenring/agent-smith:latest
    environment:
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - GITHUB_TOKEN=${GITHUB_TOKEN}
    volumes:
      - .:/app/repo
      - /var/run/docker.sock:/var/run/docker.sock
    command: ["security-scan", "--repo", "/app/repo", "--output", "console,sarif", "--output-dir", "/app/repo/results"]
```

```bash
docker compose -f docker-compose.ci.yml run --rm agentsmith
```

!!! warning "Docker socket access"
    The Docker Compose approach requires Docker socket access (`/var/run/docker.sock`) for tool containers like Nuclei and Spectral. Some CI environments restrict this.

## Pipeline-Specific Guides

- [Azure DevOps](azure-devops.md) — Pipeline tasks, `##vso` summary tabs, artifact publishing
- [GitHub Actions](github-actions.md) — Workflow steps, SARIF upload to Security tab
- [GitLab CI](gitlab-ci.md) — Job definitions, artifact reports

## Output Formats

Agent Smith supports multiple output strategies via the `--output` flag:

| Format       | Flag         | Use case                          |
|-------------|-------------|-----------------------------------|
| Console     | `console`   | Human-readable terminal output    |
| Summary     | `summary`   | Compact one-page report           |
| Markdown    | `markdown`  | Rich report for PR comments       |
| SARIF       | `sarif`     | Standard format for security tools |

Combine them: `--output console,sarif,markdown --output-dir ./results`
