namespace AgentSmith.Contracts.Services;

/// <summary>
/// Per-pipeline allow-list of tool-host types. ToolKit composes the LLM tool
/// surface by intersecting the registered IToolHost instances with the set
/// returned here. Pipeline name is the key (not pipeline type) so two
/// pipelines of the same type can declare different tool surfaces — e.g. a
/// future "invoice-processor" and "calendar-scheduler" message-family
/// pipelines can have distinct allow-lists.
/// </summary>
public interface IPipelineToolPolicy
{
    /// <summary>
    /// Set of <see cref="Type"/> values identifying which IToolHost
    /// implementations are active for <paramref name="pipelineName"/>.
    /// Unknown names should fall back to the full set, not the empty set,
    /// so test pipelines and operator-defined custom presets continue to
    /// work without explicit policy registration.
    /// </summary>
    IReadOnlySet<Type> GetAllowedHosts(string pipelineName);
}
