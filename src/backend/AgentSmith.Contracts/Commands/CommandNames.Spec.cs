namespace AgentSmith.Contracts.Commands;

/// <summary>
/// The steps that turn a ticket into the contract a run is judged by, and that decide
/// whether that contract can stand. Lifted out of the pipeline names so the three read
/// together — they are one story, and the file they were in is over its length budget.
/// </summary>
public static partial class CommandNames
{
    /// <summary>p0393a: turns any ticket into an ORDERED SET of phase specs on the ticket
    /// branch — yaml for what and done, markdown for the verbatim code templates — so the
    /// `code` pipeline runs on ordinary tickets and not only on ones an operator hand-wrote.
    /// Runs after AnalyzeCode because one of its two hand-backs ("the requirement
    /// contradicts the repository") is only findable once the code has been read. Source
    /// precedence is fixed: branch artifact, then a spec embedded in the ticket
    /// DESCRIPTION, then derivation — a ticket COMMENT is never a source. Every ticket
    /// segment is carried by a named phase or discarded with a reason; an accounting that
    /// cannot be produced does not split at all.</summary>
    public const string DeriveSpec = "DeriveSpecCommand";

    /// <summary>Challenges the derived contract against the repository before a single
    /// master token is spent on it: a criterion that prescribes the shape of the solution,
    /// or that no observation settles, is replaced by the observation that decides it or
    /// handed to the author. Runs before SpecHandback, whose park it uses.</summary>
    public const string ReviewSpec = "ReviewSpecCommand";

    /// <summary>p0393a: routes the derivation's two hand-back cases. A requirement that
    /// contradicts the repository parks in needs_clarification_status and re-triggers on an
    /// answer; not-implementable is a VERDICT — it parks in its own
    /// not_implementable_status, does NOT auto-retry on a comment and restarts only on an
    /// explicit operator Retry. Two hand-backs with the same case code and no source commit
    /// between them end the loop. No-op when the derivation handed nothing back.</summary>
    public const string SpecHandback = "SpecHandbackCommand";
}
