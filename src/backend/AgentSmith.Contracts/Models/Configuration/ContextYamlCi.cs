namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0202a: the operator-owned, durable CI block of a context.yaml. Read at
/// discovery time (the same point as <c>stack.lang</c>) so the value reaches
/// the early InstallDependencies step. Shaped so test_command / build_command
/// can join the same override path later without a reshape.
/// </summary>
/// <param name="InstallCommand">`ci.install_command:` — dependency-install
/// idiom (e.g. <c>npm ci</c>, <c>pip install -r requirements.txt</c>).
/// Empty/absent → no install step for that context.</param>
public sealed record ContextYamlCi(
    string? InstallCommand = null);
