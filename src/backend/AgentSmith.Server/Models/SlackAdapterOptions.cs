using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Models;

/// <summary>
/// Configuration for SlackAdapter. 2026-11-18-9ae8: each credential is read through the reader on every access —
/// the server's is ICredentialResolver.Named, so the environment wins and a value stored in the admin area fills
/// in without a restart. A value assigned here (a test's) wins over the reader. 2026-11-18-78efb: the reader is
/// ICredentialResolver.Lookup, so the signing secret can say that its stored value exists but cannot be opened.
/// </summary>
public sealed class SlackAdapterOptions(Func<string, CredentialLookup> lookup)
{
    public const string BotTokenVariable = "SLACK_BOT_TOKEN";
    public const string SigningSecretVariable = "SLACK_SIGNING_SECRET";

    private string? _botToken;
    private string? _signingSecret;

    public SlackAdapterOptions() : this(_ => CredentialLookup.None) { }

    public string BotToken
    {
        get => _botToken ?? lookup(BotTokenVariable).Value ?? string.Empty;
        set => _botToken = value;
    }

    public string SigningSecret
    {
        get => SigningSecretLookup.Value ?? string.Empty;
        set => _signingSecret = value;
    }

    /// <summary>The signing secret as a lookup: an assigned value, else the reader's value, none or unreadable.</summary>
    public CredentialLookup SigningSecretLookup =>
        _signingSecret is null ? lookup(SigningSecretVariable) : CredentialLookup.Of(_signingSecret);
}
