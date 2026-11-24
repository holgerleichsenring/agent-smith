using System.Security.Cryptography;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: the import server of <see cref="PocImportFixture"/> with a master key, so a secret PoC
/// variable can be stored, and every log line recorded.</summary>
public sealed class PocVariablesFixture : PocImportFixture
{
    private static readonly string Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    internal RecordingLoggerProvider Logs { get; } = new();

    protected override Action<IServiceCollection>? Services => services =>
    {
        base.Services!(services);
        // A poc- project names the target's source repo, as the provisioner writes it; a run of it needs one.
        services.RemoveAll<AgentSmith.Contracts.Services.IConfigurationLoader>();
        services.AddSingleton<AgentSmith.Contracts.Services.IConfigurationLoader>(new AgentSmith.Tests.Server.Auth.ProjectScopeUnderTest.FixedLoader(
            new AgentSmith.Contracts.Models.Configuration.AgentSmithConfig
            {
                Pocs = Pocs,
                Projects = new[] { "poc-ann", "poc-bob" }.ToDictionary(name => name, name => new AgentSmith.Contracts.Models.Configuration.ResolvedProject
                {
                    Name = name, Repos = [new AgentSmith.Contracts.Models.Configuration.RepoConnection { Name = "poc-src" }],
                }, StringComparer.OrdinalIgnoreCase),
            }));
        services.RemoveAll<MasterKeyRing>();
        services.AddSingleton(new MasterKeyRing(name => name == MasterKeyRing.Variable ? Key : null));
        services.AddSingleton<ILoggerProvider>(Logs);
        // A poc-variables run reads no skill, but its prologue resolves a catalog; no catalog download in a test.
        services.RemoveAll<AgentSmith.Contracts.Services.ISkillsCatalogResolver>();
        services.AddSingleton<AgentSmith.Contracts.Services.ISkillsCatalogResolver>(new StubSkillsCatalogResolver());
    };
}
