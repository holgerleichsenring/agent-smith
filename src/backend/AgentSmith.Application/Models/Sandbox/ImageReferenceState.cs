namespace AgentSmith.Application.Models.Sandbox;

/// <summary>2026-10-12-50bdb: what the image's registry said about it.</summary>
public enum ImageReferenceState
{
    /// <summary>The registry serves a manifest for the reference.</summary>
    Exists,

    /// <summary>The registry answered 404: it does not have the reference.</summary>
    Missing,

    /// <summary>No definite answer — credentials, a rate limit, no connection, a host not looked up.</summary>
    Unverified,
}
