using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0272: parses the operator's <c>sandbox.secrets</c> block into the validated
/// <see cref="ResolvedSandboxSecrets"/> the sandbox spec carries. Operator config
/// is fail-fast: a malformed <c>secretName:key</c> env reference throws (never a
/// silent default), since credentials are operator authority, not an LLM guess.
/// </summary>
public interface ISandboxSecretsResolver
{
    ResolvedSandboxSecrets Resolve(SandboxConfig? sandbox);
}
