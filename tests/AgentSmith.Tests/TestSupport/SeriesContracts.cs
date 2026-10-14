using AgentSmith.Application.Services.Specs;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-13-8762j: the real series contract reader over the real check.</summary>
internal static class SeriesContracts
{
    public static SeriesContractReader Reader() => new(new SeriesContractCheck());
}
