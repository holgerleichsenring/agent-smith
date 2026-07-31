namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: one technical RULE carried verbatim out of the ticket. One rule, one
/// home: the rule text lives here in spec.yaml, its SAMPLE (code template,
/// config block, reference snippet) lives in spec.md and is referenced by
/// <see cref="SampleAnchor"/> — never inlined here, never generated from here.
/// </summary>
public sealed record WorkSpecConstraint(string Rule, string? SampleAnchor = null);
