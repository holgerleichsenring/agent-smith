namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: one booted server per auth configuration, shared by the cases that assert about
/// it. Booting the real composition costs seconds, and the cases in a class differ only in
/// what they ASK the same server — the configuration is the unit, so the fixture is too.
/// </summary>
public abstract class AuthorityFixture : IAsyncLifetime
{
    /// <summary>The audience the enforcing configurations demand, and tests can get wrong.</summary>
    public const string Audience = "agent-smith-under-test";

    private readonly List<string> _tempFiles = [];

    public LocalTokenIssuer Issuer { get; private set; } = null!;

    public BootedServer Server { get; private set; } = null!;

    /// <summary>The <c>auth:</c> block this configuration writes, or nothing at all.</summary>
    protected abstract string AuthYaml(string authority);

    public async Task InitializeAsync()
    {
        Issuer = await LocalTokenIssuer.StartAsync();
        var dbPath = Temp("db");
        _tempFiles.Add(dbPath + "-wal");
        _tempFiles.Add(dbPath + "-shm");
        var configPath = Temp("yml");
        await File.WriteAllTextAsync(configPath, $"""
            persistence:
              provider: sqlite
              connection_string: Data Source={dbPath}

            """ + AuthYaml(Issuer.Authority));
        Server = await BootedServer.StartAsync(configPath);
    }

    public async Task DisposeAsync()
    {
        await Server.DisposeAsync();
        await Issuer.DisposeAsync();
        foreach (var file in _tempFiles)
            if (File.Exists(file)) File.Delete(file);
    }

    private string Temp(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-auth-{Guid.NewGuid():N}.{extension}");
        _tempFiles.Add(path);
        return path;
    }
}
