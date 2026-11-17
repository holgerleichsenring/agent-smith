namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-11-17-ec48a: how the one review of a master's spec revision ended.</summary>
public enum SpecRevisionOutcome
{
    /// <summary>BUILD, or BUILD WITH CUTS with every cut applied: the text may be written.</summary>
    Cleared,

    /// <summary>A reviewer refused the text; its findings go back to the master.</summary>
    Refused,

    /// <summary>A cited fact did not resolve against the tree; no reviewer was asked.</summary>
    Unresolved,

    /// <summary>No reviewer answered.</summary>
    NotTaken,

    /// <summary>The reviser could not apply the cuts.</summary>
    CutsNotApplied,
}
