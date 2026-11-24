namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// 2026-10-02-5f89b: one step of testing an unsaved connection or tracker against its host —
/// secret, host, identity, scope, then repos or open tickets. <see cref="Detail"/> is a sentence
/// of ours or a parsed field (a login, a display name, a count); never a response body, never a
/// token.
/// </summary>
public sealed record DraftCheckStep(string Key, string Label, bool Ok, string Detail)
{
    public const string Secret = "secret";
    public const string Host = "host";
    public const string Identity = "identity";
    public const string Scope = "scope";
    public const string Repos = "repos";
    public const string Tickets = "tickets";
    public const string Time = "time";
    public const string Values = "values"; // 2026-11-23-933de: the draft's own values, checked before any request
    public const string Token = "token"; // 2026-11-23-c81ec: an Argo CD role token's expiry
    public const string ApplicationSet = "application_set"; // 2026-11-23-c81ec: the target's ApplicationSet as Argo CD holds it

    public static DraftCheckStep Pass(string key, string detail) => new(key, LabelOf(key), true, detail);

    public static DraftCheckStep Fail(string key, string detail) => new(key, LabelOf(key), false, detail);

    private static string LabelOf(string key) => key switch
    {
        Secret => "Secret",
        Host => "Host",
        Identity => "Identity",
        Scope => "Scope",
        Repos => "Repositories",
        Tickets => "Open tickets",
        Values => "Values",
        Token => "Token",
        ApplicationSet => "ApplicationSet",
        _ => "Time budget",
    };
}
