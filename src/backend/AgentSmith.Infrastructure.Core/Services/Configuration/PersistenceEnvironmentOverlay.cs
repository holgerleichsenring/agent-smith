using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-09-04-102b: lays <see cref="PersistenceEnvKeys"/> over the persistence block the
/// configuration file declared. A cluster injects a database credential the way it injects
/// every other secret — an environment variable fed from a Secret — and a mounted ConfigMap,
/// which is shared and not secret, is the wrong place to keep one.
/// <para>
/// The pair moves together. The provider chooses the GRAMMAR of the connection string, so a
/// connection taken from the environment while the provider stays with the file would hand a
/// <c>Host=…</c> to SQLite — a throw out of the DbContext factory at first use, which is the
/// failure shape p0391b already paid for. Exactly one variable set therefore changes nothing
/// and is recorded as the configuration error it is: the file's coherent pair keeps running,
/// and every process that reads it still agrees on which database that is.
/// </para>
/// </summary>
public sealed class PersistenceEnvironmentOverlay(IStartupFindings? findings = null)
{
    private readonly IStartupFindings _findings = findings ?? new StartupFindings();

    /// <summary>The persistence block to use: the environment's pair, or the declared one.</summary>
    public PersistenceConfig Apply(PersistenceConfig? declared)
    {
        var provider = Read(PersistenceEnvKeys.Provider);
        var connection = Read(PersistenceEnvKeys.Connection);
        if (provider is not null && connection is not null)
            return new PersistenceConfig { Provider = provider, ConnectionString = connection };
        if (provider is null != connection is null)
            RecordHalfPair(
                missing: provider is null ? PersistenceEnvKeys.Provider : PersistenceEnvKeys.Connection,
                present: provider is null ? PersistenceEnvKeys.Connection : PersistenceEnvKeys.Provider);
        return declared ?? new PersistenceConfig();
    }

    private void RecordHalfPair(string missing, string present) =>
        _findings.Record(new StartupFinding(
            StartupSubsystems.ConfigFile,
            StartupFindingSeverity.Blocking,
            $"{present} is set but {missing} is not, so NEITHER is in use and the database named "
            + "in the configuration file is. The two are read together because the provider "
            + $"decides how the connection string is parsed. Set {missing} as well.",
            Field: missing));

    private static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
