using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0349: reads ONLY the bootstrap slice (persistence + secret names, p0503b: + the auth
/// block) from the agentsmith.yml at <see cref="IConfigStoreLocation.ConfigPath"/>. This is
/// what wires the DB connection before the rest of the config is loaded from that DB.
/// A missing/unparseable file yields defaults so the server can still boot
/// unconfigured (sqlite default) and the DI graph validates without a file present.
/// </summary>
public sealed class BootstrapConfigReader(IConfigStoreLocation location,
    RawConfigYaml rawConfigYaml,
    AuthEnvironmentOverlay authEnvironment,
    PersistenceEnvironmentOverlay persistenceEnvironment,
    IStartupFindings? findings = null)
{
    private readonly IStartupFindings _findings = findings ?? new StartupFindings();

    public BootstrapConfig Read()
    {
        var raw = ReadFile();
        // p0503b: the environment is read even when there is no file — a cluster can carry
        // the whole auth block in variables, and a file that failed to parse must not take
        // the authority down with it.
        var auth = authEnvironment.Apply(raw?.Auth);
        RecordIfUnusable(auth);
        // 2026-09-04-102b: persistence is read from the environment on the same terms and for
        // the same reason — a cluster carries the database in a Secret, and a file that failed
        // to parse must not silently retarget the server to the built-in SQLite default.
        var persistence = persistenceEnvironment.Apply(raw?.Persistence);
        return raw is null
            ? BootstrapConfig.Default() with { Persistence = persistence, Auth = auth }
            : new BootstrapConfig(persistence, raw.Secrets, auth);
    }

    private RawAgentSmithConfig? ReadFile()
    {
        var path = location.ConfigPath;
        if (!File.Exists(path)) return null;
        try
        {
            return rawConfigYaml.Deserialize(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            // p0391b: the catch used to be YamlException only, so a file that EXISTS but
            // cannot be READ — wrong owner on a mounted ConfigMap, a directory where a file
            // was expected — threw out of the DbContext factory and killed the server on the
            // first scope. Any failure to read the bootstrap slice is now the same finding.
            _findings.Record(Unreadable(path, ex));
            return null;
        }
    }

    /// <summary>
    /// p0503b: the YAML loader ignores unmatched properties, so a misspelled key under
    /// <c>auth:</c> yields no authority, no error, and a server that quietly stays open.
    /// ADVISORY and deliberately project-less: a blocking finding would report the whole
    /// installation degraded, and a finding that named a project would disable that
    /// project's triggers — neither is true of an installation that simply has no
    /// authentication, which is exactly the state it was in before the block was written.
    /// </summary>
    private void RecordIfUnusable(TokenAuthorityConfig? auth)
    {
        if (auth is null || auth.IsUsable) return;
        _findings.Record(new StartupFinding(
            StartupSubsystems.Auth,
            StartupFindingSeverity.Advisory,
            "An auth block is configured but names no authority, so no token is validated "
            + "and every route answers unauthenticated. Check the spelling of the keys under "
            + $"'auth:' in the config file, or set {AuthEnvironmentOverlay.AuthorityVar}.",
            Field: "authority"));
    }

    private static StartupFinding Unreadable(string path, Exception ex) => new(
        StartupSubsystems.ConfigFile,
        StartupFindingSeverity.Blocking,
        $"The bootstrap config at '{path}' could not be read, so the built-in defaults "
        + "(sqlite, no secret names) are in use and the configured database is NOT being "
        + $"used. Cause: {ex.Message}",
        Field: "CONFIG_PATH");
}
