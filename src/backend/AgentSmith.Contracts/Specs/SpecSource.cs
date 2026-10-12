namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: where the set the run works from came from. The precedence is fixed and
/// a ticket COMMENT is deliberately not among them: after the first run the ticket
/// carries the derived set as a comment, so a rule reading "a ticket carrying a
/// spec skips derivation" would feed the run its own echo — and a comment is
/// editable by anyone, which is precisely the third-party input derivation exists
/// to bound.
/// </summary>
public enum SpecSource
{
    /// <summary>Read back from the ticket branch — the winner whenever it exists.</summary>
    BranchArtifact = 0,

    /// <summary>Derived from the ticket prose against the analysed repository.</summary>
    Derived = 2,

    /// <summary>
    /// 2026-09-17-0e79a: approved by a person in the design conversation and carried to the run
    /// that works the ticket it filed. 2026-10-06-03c7f: it is used ONLY while the ticket branch
    /// holds nothing at the series' path — the run then publishes it there. A branch that answered
    /// is the set whatever the approval instants say, and an approved set is re-cut only on a
    /// person's demand.
    /// </summary>
    Approved = 3,
}
