using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.TestSupport;

internal sealed class NullBaselineLoader : IBaselineLoader
{
    public string? Load(string baselineName) => null;
}
