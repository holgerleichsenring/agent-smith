namespace AgentSmith.Domain.Models;

public sealed record CiConfig(
    bool HasCi,
    string? BuildCommand,
    string? TestCommand,
    string? CiSystem,
    // p0202e: the analyzer-DERIVED command to prepare the environment before
    // tests (e.g. "npm install" — or "npm ci" ONLY when a package-lock.json is
    // committed; "pip install -r requirements.txt"; "go mod download"). Derived
    // from what is actually in the repo, so it self-heals against repo state
    // (the npm-ci-without-lock failure class). The install step uses this unless
    // the operator set an override in context.yaml.
    string? InitializeCommand = null);
