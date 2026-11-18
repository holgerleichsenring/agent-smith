using System.Security.Cryptography;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-11-17-f3a8d: an enforcing server with a migrated store, a master key of its own (stated, not read from the
/// process environment) and every log entry recorded — so a case can look for a secret value everywhere it could leak.
/// </summary>
public sealed class SecretStoreAuthorityFixture : AuthorityFixture
{
    private static readonly string Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    internal RecordingLoggerProvider Logs { get; } = new();

    protected override bool MigratesStore => true;

    protected override Action<IServiceCollection>? Services => services =>
    {
        services.RemoveAll<MasterKeyRing>();
        services.AddSingleton(new MasterKeyRing(name => name == MasterKeyRing.Variable ? Key : null));
        services.AddSingleton<ILoggerProvider>(Logs);
    };

    protected override string AuthYaml(string authority) => $"""
        auth:
          authority: {authority}
          audience: {Audience}
          enforce: true
          role_claim: roles
          name_claim: sub
        """;
}
