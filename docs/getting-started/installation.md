# Installation

## Download Binary (recommended)

Self-contained single-file executables. No .NET runtime required.

=== "Linux (x64)"

    ```bash
    curl -sL https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64 \
      -o /usr/local/bin/agent-smith && chmod +x /usr/local/bin/agent-smith
    ```

=== "Linux (ARM64)"

    ```bash
    curl -sL https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-arm64 \
      -o /usr/local/bin/agent-smith && chmod +x /usr/local/bin/agent-smith
    ```

=== "macOS (Apple Silicon)"

    ```bash
    curl -sL https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-osx-arm64 \
      -o /usr/local/bin/agent-smith && chmod +x /usr/local/bin/agent-smith
    ```

=== "macOS (Intel)"

    ```bash
    curl -sL https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-osx-x64 \
      -o /usr/local/bin/agent-smith && chmod +x /usr/local/bin/agent-smith
    ```

=== "Windows"

    ```powershell
    Invoke-WebRequest -Uri https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-win-x64.exe `
      -OutFile agent-smith.exe
    ```

All binaries are available on the [Releases page](https://github.com/holgerleichsenring/agent-smith/releases).

## Docker

```bash
docker pull holgerleichsenring/agent-smith:latest
```

The Docker image automatically fixes volume mount permissions — no manual `chmod` needed.

## Build from Source

```bash
git clone https://github.com/holgerleichsenring/agent-smith.git
cd agent-smith
dotnet build
dotnet run --project src/AgentSmith.Host -- --help
```

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Verify Installation

```bash
agent-smith --help
```

You should see the banner and a list of available commands:

```
agent-smith fix            # Fix a bug (plan, execute, test, PR)
agent-smith feature        # Add a feature
agent-smith init           # Bootstrap a new project
agent-smith mad            # Multi-agent design discussion
agent-smith legal          # Analyze a legal document
agent-smith security-scan  # Scan code for security vulnerabilities
agent-smith api-scan       # Scan a running API
agent-smith server         # Start webhook listener
```
