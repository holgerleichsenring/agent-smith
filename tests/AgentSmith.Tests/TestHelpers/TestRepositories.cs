using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>2026-10-12-f5f7c: a one-repository run over a given file surface.</summary>
internal static class TestRepositories
{
    internal static IRunRepositories One(ISandboxFileReader reader, string name = "repo") =>
        new FixedRunRepositories(new Dictionary<string, ISandboxFileReader> { [name] = reader });
}
