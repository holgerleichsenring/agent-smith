namespace AgentSmith.Contracts.Services;

/// <summary>2026-11-18-78efb: what looking up a named credential found.</summary>
public enum CredentialLookupState
{
    /// <summary>A value: the environment variable's, or the catalog secret's.</summary>
    Value,

    /// <summary>No value configured anywhere: the variable is unset and the catalog secret has none.</summary>
    None,

    /// <summary>The variable is unset and the catalog secret's stored value exists but cannot be opened.</summary>
    Unreadable,
}
