namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-01-283da: one file an operator handed a design conversation — a screenshot, or one
/// file of an uploaded website — keyed on the conversation's SESSION id like the images before
/// it, and grouped by the set it arrived in.
/// <para>
/// THE BYTES ARE BYTES. The legacy image table held base64 text because no byte store existed;
/// that cost a third more on disk and a decode on every design turn. This is that store.
/// </para>
/// <para>
/// ONE ID SPACE. The identity starts at <see cref="Models.ReferenceFileIdentity.Seed"/>, above
/// every legacy image id, so a legacy image copied in keeps its number and a page that addressed
/// it before the upgrade still addresses the same image after it.
/// </para>
/// </summary>
public sealed class ReferenceFile : EntityBase
{
    public long Id { get; set; }

    /// <summary>The conversation's session id — the delete sweeps by this.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The upload this file arrived in; an image is a set of one.</summary>
    public string SetId { get; set; } = string.Empty;

    /// <summary>One of <see cref="Models.ReferenceFileKind"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The file's path inside its set, forward slashes; empty for an image.</summary>
    public string RelativePath { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public long Length { get; set; }

    /// <summary>The file itself. Unbounded and untyped by design — see the configuration.</summary>
    public byte[] Content { get; set; } = [];
}
