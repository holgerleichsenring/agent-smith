using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-10-12-6bfaa: the resolver with its real collaborators — the branch locator and the
/// context.yaml reader — over a test's source factory and parser.
/// </summary>
internal static class TestSandboxLanguageResolver
{
    public static SandboxLanguageResolver Create(ISourceProviderFactory factory, IContextYamlParser parser) =>
        new(factory,
            new ContextBranchLocator(NullLogger<ContextBranchLocator>.Instance),
            new ContextYamlRemoteReader(parser, NullLogger<ContextYamlRemoteReader>.Instance),
            NullLogger<SandboxLanguageResolver>.Instance);
}
