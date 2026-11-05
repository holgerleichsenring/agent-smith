namespace AgentSmith.Contracts.Models;

/// <summary>2026-11-05-862ea: the kind of undertaking a work picture names; not the derivation's WorkShape.</summary>
public enum UndertakingKind
{
    /// <summary>Building again what an existing truth (an upload, a legacy implementation) already describes.</summary>
    Rebuild,

    /// <summary>New behaviour in code that exists.</summary>
    Feature,

    /// <summary>A defect in code that exists.</summary>
    Fix,

    /// <summary>Writing documentation as the work itself — not the document outcome a conversation can end in.</summary>
    Document,
}
