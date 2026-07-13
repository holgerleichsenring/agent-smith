using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0327: round-trips the DATA entries of a PipelineContext for a run
/// checkpoint. Live infrastructure objects (sandbox handles, the sandbox
/// coordinator, pricing resolvers, catalog bindings) are excluded and
/// re-established on resume by the normal pipeline seeding + re-provisioning.
/// </summary>
public interface IPipelineContextSerializer
{
    string Serialize(PipelineContext context);

    /// <summary>Applies serialized entries onto a freshly seeded context.
    /// Restored entries win over standard seeding (e.g. a ScopeRepos-narrowed
    /// repo list); entries that no longer deserialize are skipped with a
    /// warning rather than failing the resume.</summary>
    void Restore(string serialized, PipelineContext into);
}
