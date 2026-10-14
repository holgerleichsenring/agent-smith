namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-8762h: which question a <see cref="SpecHandbackCase.Question"/> hand-back asks. The case stays
/// Question for every kind, because the taken reading and the unanswered-question pin are gated on it; the
/// kind decides the comment, its marker and how the next run reads the silence. Persisted by name: append,
/// never rename.
/// </summary>
public enum SpecQuestionKind
{
    /// <summary>The ticket reads two ways and the work differs between them.</summary>
    Reading = 0,

    /// <summary>A reviewer's REFUSE stood after the review rounds; the author is asked once whether the spec
    /// is built as reviewed.</summary>
    ReviewObjection = 1,
}
