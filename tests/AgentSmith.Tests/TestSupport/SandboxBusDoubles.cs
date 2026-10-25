using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Server.Services.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-25-c5a7e: the sandbox bus credential for tests that are not about it.</summary>
internal static class SandboxBusDoubles
{
    internal static SandboxBusCredential SharedCredential { get; } = SandboxBusCredential.Shared("redis:6379");

    internal static SandboxBusCredentials Credentials(
        SandboxBusCredentialMode mode = SandboxBusCredentialMode.Shared, IConnectionMultiplexer? redis = null) =>
        new(redis ?? Mock.Of<IConnectionMultiplexer>(), new SandboxBusCredentialSetting(mode),
            new StartupFindings(), NullLogger<SandboxBusCredentials>.Instance);
}
