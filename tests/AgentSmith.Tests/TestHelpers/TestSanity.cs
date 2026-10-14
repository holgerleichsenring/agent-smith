using AgentSmith.Application.Services.Loop.Sanity;
using AgentSmith.Application.Services.Specs;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>The real tool-result sanity collaborators, the four rules in their registered order.</summary>
internal static class TestSanity
{
    public static ToolResultSanity Sanity() =>
        new([new EmptySubAgentAnswerRule(), new UncitedZeroHitRule(), new UnnamedRepositoryWriteRule(), new OutsideWorkCommandRule()],
            NullLogger<ToolResultSanity>.Instance);

    public static ToolResultSanityScopeReader Reader() => new(new EvidenceReferences());

    public static ToolResultSanityWrapper Wrapper() => new(Sanity(), Reader());
}
