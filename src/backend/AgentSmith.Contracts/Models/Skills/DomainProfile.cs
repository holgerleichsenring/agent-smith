namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// p0504: what one word in a repository's <c>meta.domain</c> brings — a
/// toolchain image and an ordered list of verification commands, authored once
/// in the skills catalog under <c>profiles/&lt;name&gt;/profile.yaml</c> and
/// shipped in the pinned tarball.
/// </summary>
/// <param name="Name">The declared domain; equals the profile's directory name.</param>
/// <param name="Image">The toolchain image the profile's commands were written for.
/// Used only when the context declares no image of its own.</param>
/// <param name="CompatibleImages">Images the profile author states carry the same
/// tools. A context image outside this list still wins (the operator's rule), but
/// the mismatch is reported rather than discovered as "command not found".</param>
/// <param name="Verify">The ordered verification commands, run in the sandbox
/// when the repository declares none of its own.</param>
public sealed record DomainProfile(
    string Name,
    string Image,
    IReadOnlyList<string> CompatibleImages,
    IReadOnlyList<DomainProfileCommand> Verify);
