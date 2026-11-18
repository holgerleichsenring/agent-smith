namespace AgentSmith.Server.Models;

/// <summary>
/// Configuration for SlackAdapter. 2026-11-18-9ae8: each credential is read through the reader on every access —
/// the server's is ICredentialResolver.Named, so the environment wins and a value stored in the admin area fills
/// in without a restart. A value assigned here (a test's) wins over the reader.
/// </summary>
public sealed class SlackAdapterOptions(Func<string, string?> read)
{
    public const string BotTokenVariable = "SLACK_BOT_TOKEN";
    public const string SigningSecretVariable = "SLACK_SIGNING_SECRET";

    private string? _botToken;
    private string? _signingSecret;

    public SlackAdapterOptions() : this(_ => null) { }

    public string BotToken
    {
        get => _botToken ?? read(BotTokenVariable) ?? string.Empty;
        set => _botToken = value;
    }

    public string SigningSecret
    {
        get => _signingSecret ?? read(SigningSecretVariable) ?? string.Empty;
        set => _signingSecret = value;
    }
}
