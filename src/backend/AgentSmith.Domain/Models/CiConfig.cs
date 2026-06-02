namespace AgentSmith.Domain.Models;

public sealed record CiConfig(
    bool HasCi,
    string? BuildCommand,
    string? TestCommand,
    string? CiSystem,
    // p0202/p0202a: the analyzer's DETECTED dependency-install command
    // (e.g. "npm ci", "pip install -r requirements.txt"). This is the
    // suggestion bootstrap seeds into context.yaml; the runtime InstallDependencies
    // step reads the durable, operator-owned value from context.yaml at discovery
    // time (RemoteContextDiscovery.InstallCommand), NOT this field. Free text —
    // the framework never sniffs for language. Defaulted so existing four-arg
    // construction sites keep compiling.
    string? InstallCommand = null);
