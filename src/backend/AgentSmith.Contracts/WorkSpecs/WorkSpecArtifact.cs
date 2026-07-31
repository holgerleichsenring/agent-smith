namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: the pair that lives on the ticket branch — spec.yaml and its authored
/// companion spec.md. Neither file is generated from the other: revising the
/// yaml never rewrites the md, and when a revision invalidates a sample the
/// revision says so in its cause and the md is edited in the same commit.
/// </summary>
public sealed record WorkSpecArtifact(WorkSpec Spec, string SamplesMarkdown);
