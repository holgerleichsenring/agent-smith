# CI/CD Integration

Agent Smith can run inside your CI/CD pipeline to scan the code of every build (`security-scan`) or a running API against its OpenAPI description (`api-scan`). The CLI runs one scan, writes its reports and exits.

## What a CI run needs

A configuration file with one agent in it. Commit it to the repository, for example as `ci/agentsmith.yml`:

```yaml
agents:
  ci-scan:
    type: claude
    model: claude-sonnet-4-6
```

The `claude` agent reads `ANTHROPIC_API_KEY` from the environment, so set that as a masked CI secret. Any other agent type works the same way with its own key (`OPENAI_API_KEY`, `AZURE_OPENAI_API_KEY`, `GEMINI_API_KEY`); see [AI providers](../../connect-your-stuff/ai-providers.md).

Without `--config`, the CLI looks for `./.agentsmith/agentsmith.yml`, then `./config/agentsmith.yml`, then `~/.agentsmith/agentsmith.yml`. The examples on these pages pass `--config` explicitly.

`security-scan`, and `api-scan` when you give it source, also need the scanned repository to carry its `.agentsmith/` context files (a `context.yaml` and a `principles.md`). Without them the scan stops at its bootstrap gate. See [Context file](../concepts/context-file.md).

## Binary approach (recommended)

Download the self-contained binary for your runner's platform. No Docker and no .NET runtime are needed: the CLI runs its sandbox in-process, on the runner itself.

```bash
curl -fsSL -o agent-smith \
  https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64
chmod +x agent-smith

# Scan the checked-out repository
./agent-smith security-scan \
  --config ci/agentsmith.yml \
  --agent ci-scan \
  --source-path . \
  --output console,sarif,markdown \
  --output-dir ./results
```

Available platforms:

| Platform         | Binary name                   |
|------------------|-------------------------------|
| Linux x64        | `agent-smith-linux-x64`       |
| Linux ARM64      | `agent-smith-linux-arm64`     |
| macOS x64        | `agent-smith-osx-x64`         |
| macOS ARM64      | `agent-smith-osx-arm64`       |
| Windows x64      | `agent-smith-win-x64.exe`     |

`latest` follows every release. Pin a version in a pipeline you depend on by replacing `latest/download` with `download/v<version>`.

!!! tip "Why binary over Docker?"
    The binary is faster (no image pull), simpler (no Docker-in-Docker), and works on any runner that can execute a native binary. `api-scan` runs Nuclei, Spectral and ZAP in containers when a Docker or Podman socket is available and falls back to local processes otherwise, see [Tool configuration](../configuration/tools.md).

## Docker approach

The CLI is also published as the `holgerleichsenring/agent-smith-cli` image. Its entrypoint is the CLI, so the container arguments are the command line.

```bash
docker run --rm \
  -e ANTHROPIC_API_KEY \
  -v "$PWD":/repo \
  -v "$PWD/results":/output \
  -v /var/run/docker.sock:/var/run/docker.sock \
  holgerleichsenring/agent-smith-cli:latest \
  security-scan --config /repo/ci/agentsmith.yml --agent ci-scan \
    --source-path /repo --output console,sarif,markdown --output-dir /output
```

!!! warning "Docker socket access"
    Mounting `/var/run/docker.sock` lets `api-scan` start its tool containers (Nuclei, Spectral, ZAP) next to the CLI container. Some CI environments restrict this. `security-scan` does not need it.

## Pipeline-specific guides

- [Azure DevOps](azure-devops.md): pipeline tasks, `##vso` summary tabs, artifact publishing
- [GitHub Actions](github-actions.md): workflow steps, SARIF upload to the Security tab
- [GitLab CI](gitlab-ci.md): job definitions, artifact reports

## Output formats

The `--output` flag takes a comma-separated list. The default is `console`.

| Format      | Flag        | What it produces                                   |
|-------------|-------------|----------------------------------------------------|
| Console     | `console`   | Findings printed to stdout                         |
| Summary     | `summary`   | A compact findings summary on stdout               |
| Markdown    | `markdown`  | `findings.md` in the output directory              |
| SARIF       | `sarif`     | `findings.sarif` (SARIF 2.1.0) in the output directory |

The output directory is the first writable one of `--output-dir`, `/output` and `./agentsmith-output`.

## Exit codes and gating

The command exits with 1 when the run fails and 0 otherwise. Findings alone do not change the exit code. To fail a build on findings, read the SARIF file: Critical and High findings carry the level `error`, Medium `warning`, Low `note`. Each guide shows a gate step that does this with `jq`.
