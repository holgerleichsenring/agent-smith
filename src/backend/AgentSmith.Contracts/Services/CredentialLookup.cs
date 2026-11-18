namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-18-78efb: a named credential's lookup — a value, none, or a stored value that exists but cannot be opened
/// (a lost or rotated master key, an unreachable vault, a tampered row). The three are told apart by
/// <see cref="State"/>, never by an empty string: a signature check falls open on none and refuses on unreadable.
/// Its text never carries the value.
/// </summary>
public sealed class CredentialLookup
{
    private CredentialLookup(CredentialLookupState state, string? value, string? secret, string? reason)
    {
        State = state;
        Value = value;
        Secret = secret;
        Reason = reason;
    }

    public static CredentialLookup None { get; } = new(CredentialLookupState.None, null, null, null);

    public CredentialLookupState State { get; }

    /// <summary>The value; null unless <see cref="State"/> is Value.</summary>
    public string? Value { get; }

    /// <summary>The secret whose stored value cannot be opened; null unless unreadable.</summary>
    public string? Secret { get; }

    /// <summary>Why it cannot be opened, naming no value; null unless unreadable.</summary>
    public string? Reason { get; }

    public static CredentialLookup Of(string? value) =>
        string.IsNullOrEmpty(value) ? None : new(CredentialLookupState.Value, value, null, null);

    public static CredentialLookup Unreadable(string secret, string reason) =>
        new(CredentialLookupState.Unreadable, null, secret, reason);

    public override string ToString() => State switch
    {
        CredentialLookupState.Value => "a value (not shown)",
        CredentialLookupState.Unreadable => $"the stored value of secret '{Secret}' cannot be read: {Reason}",
        _ => "no value configured",
    };
}
