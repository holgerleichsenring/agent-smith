using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-6c46: the REAL resource resolver with the real context-block acceptance
/// behind it — the pair DI composes. Tests that want an LLM-authored context.yaml
/// stack.resources block to actually flow into a spec build this, not the stub.
/// </summary>
internal static class WiredResourceResolver
{
    public static SandboxResourceResolver Create(SandboxOptions? options = null)
    {
        var opts = Options.Create(options ?? new SandboxOptions());
        return new SandboxResourceResolver(opts, new ContextResourceAcceptance(opts));
    }
}
