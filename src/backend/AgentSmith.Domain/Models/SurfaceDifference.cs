namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-c6ec: one capability the interface offers that no first-party client was
/// found to exercise — an OBSERVATION, paired with the requirement that would decide
/// whether it matters.
/// <para>
/// An unexercised operation behind a correct function-level check is untidy, not
/// dangerous. <paramref name="RequirementId"/> names the entry of the shipped verification
/// standard that settles which of the two this is; it means nothing without the catalogue
/// version the report carries.
/// </para>
/// </summary>
public sealed record SurfaceDifference(
    SurfaceDifferenceKind Kind,
    string Operation,
    string? Property,
    string RequirementId);
