namespace AgentSmith.Server.Models;

/// <summary>
/// Configuration for TeamsAdapter. The app and tenant ids are identifiers, read from the environment at composition.
/// 2026-11-18-9ae8: the app password is read through the reader on every access (the server's is
/// ICredentialResolver.Named); the bot token cached from it lives until it expires. An assigned value wins.
/// </summary>
public sealed class TeamsAdapterOptions(Func<string, string?> read)
{
    public const string AppPasswordVariable = "TEAMS_APP_PASSWORD";

    private string? _appPassword;

    public TeamsAdapterOptions() : this(_ => null) { }

    public string AppId { get; set; } = string.Empty;

    public string AppPassword
    {
        get => _appPassword ?? read(AppPasswordVariable) ?? string.Empty;
        set => _appPassword = value;
    }

    public string TenantId { get; set; } = string.Empty;
}
