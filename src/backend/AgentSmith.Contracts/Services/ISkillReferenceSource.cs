namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0313b: supplies the shared methodology text a master cites as
/// <c>{{ref:&lt;slug&gt;}}</c>. The catalog ships one file per reference under
/// <c>references/</c>, so the spawn-budget policy (and the rest) exists once and
/// every citing master renders the same words.
/// </summary>
public interface ISkillReferenceSource
{
    /// <summary>
    /// The reference body, or <c>null</c> when the loaded catalog does not ship it.
    /// Null is not a silent default: <see cref="ISkillBodyResolver"/> turns it into a
    /// loud failure, because a master that cites a reference and renders without it
    /// would go to the model missing the rules it was written to carry.
    /// </summary>
    string? TryRead(string slug);
}
