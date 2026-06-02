namespace AgentSmith.Domain.Models;

public sealed record CiConfig(
    bool HasCi,
    string? BuildCommand,
    string? TestCommand,
    string? CiSystem,
    // p0202: language-agnostic dependency-install command (e.g. "npm ci",
    // "pip install -r requirements.txt", "go mod download"). Free text — the
    // framework never sniffs for language. Defaulted so existing four-arg
    // construction sites keep compiling. Null/empty means "no install step".
    string? InstallCommand = null);
