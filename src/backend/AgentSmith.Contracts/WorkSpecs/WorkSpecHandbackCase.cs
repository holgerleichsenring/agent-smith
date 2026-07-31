namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: the enumerable hand-back cases. Unresolved points are recorded
/// ASSUMPTIONS, not a park signal — parking on anything unresolved would fire on
/// nearly every ticket and teach the operator to ignore the signal. Only these
/// three hand back, and they are a CASE CODE so non-progress can be compared
/// mechanically across runs instead of by diffing LLM-written prose.
/// </summary>
public enum WorkSpecHandbackCase
{
    /// <summary>No hand-back — the normal case.</summary>
    None = 0,

    /// <summary>The ticket cannot be read as a statement of work at all.</summary>
    NotUnderstood = 1,

    /// <summary>The ticket is readable but contradicts what the code actually is.</summary>
    RequirementsDoNotMatchTheCode = 2,

    /// <summary>A VERDICT, not a question: this cannot be built as asked.</summary>
    NotImplementable = 3,
}
