# Single Binary

Agent Smith ships as a self-contained executable for five platforms. No .NET runtime, no Docker, no dependencies.

## Download

```bash
# Linux x64
curl -fsSL -o agent-smith \
  https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
chmod +x agent-smith

# Linux ARM64
curl -fsSL -o agent-smith \
  https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-arm64
chmod +x agent-smith

# macOS Apple Silicon
curl -fsSL -o agent-smith \
  https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-osx-arm64
chmod +x agent-smith

# macOS Intel
curl -fsSL -o agent-smith \
  https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-osx-x64
chmod +x agent-smith

# Windows x64 (PowerShell)
Invoke-WebRequest -Uri "https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-win-x64" -OutFile agent-smith.exe
```

## Available Platforms

| Platform       | Binary                    | Runner examples |
|---------------|---------------------------|-----------------|
| `linux-x64`   | `agent-smith-linux-x64`   | GitHub Actions, GitLab CI, Azure Pipelines |
| `linux-arm64`  | `agent-smith-linux-arm64` | AWS Graviton, ARM runners |
| `osx-x64`     | `agent-smith-osx-x64`     | macOS Intel |
| `osx-arm64`   | `agent-smith-osx-arm64`   | macOS Apple Silicon |
| `win-x64`     | `agent-smith-win-x64`     | Windows runners, local dev |

## Configuration Discovery

The binary looks for configuration in this order:

1. **`--config` flag** — explicit path: `./agent-smith run --config /path/to/config.yml`
2. **`.agentsmith/agentsmith.yml`** — project-local (next to `.git/`)
3. **`config/agentsmith.yml`** — relative to working directory
4. **`~/.agentsmith/agentsmith.yml`** — user home fallback

!!! tip "Zero-config mode"
    For security scans and API audits, you only need the `ANTHROPIC_API_KEY` environment variable. No config file required.

## Usage Examples

### Fix a Bug

```bash
export ANTHROPIC_API_KEY=sk-ant-...
export GITHUB_TOKEN=ghp_...

./agent-smith fix --repo https://github.com/org/repo --ticket 42
```

### Security Scan

```bash
export ANTHROPIC_API_KEY=sk-ant-...

./agent-smith security-scan --repo . --output console,sarif --output-dir ./results
```

### API Security Scan

```bash
./agent-smith api-scan --repo . --output console,summary --output-dir ./results
```

### Webhook Server

```bash
./agent-smith server --port 8081
```

`REDIS_URL` is optional. The server starts even when Redis is missing or unreachable — the
webhook listener stays up and `/health` reports the degraded state. See
[Server Resilience](../operations/server-resilience.md) for `/health` and `/health/ready`
semantics.

## When to Use

- **CI/CD pipelines** — download, scan, discard. No image pull overhead.
- **Local development** — quick scans without Docker.
- **Ephemeral environments** — no installation, no cleanup.
- **Air-gapped systems** — copy the binary, set env vars, run.

## When to Use Docker Instead

- You need tool containers (Nuclei for active scanning, Spectral for API linting).
- You want the full stack (agent + webhook server + Redis + dispatcher).
- You need persistent state across runs.
