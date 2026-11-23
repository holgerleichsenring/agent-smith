using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-08-25-1806: the mapping in force, asked per request. p0503d captured it at startup
/// in singleton readers, so a team that changed cost a pod restart — while p0349 had put the
/// configuration in the database and p0353 had made it apply live.
/// <para>
/// The store answers; while it holds no mapping, or cannot be read, the bootstrap SEED does — an
/// installation whose mapping is still in its file keeps the mapping it has. 2026-11-22-747da: the
/// import that copies the seed into the store is a data migration of `database migrate`; it used to
/// run at startup and switch this source over (AdoptStore), and the switch changed nothing the
/// store-else-seed read does not already do.
/// </para>
/// </summary>
internal sealed class RoleMappingSource(IStoredRoleMapping stored, TokenAuthorityConfig auth)
{
    private readonly RoleMappingConfig _seed = RoleMappingConfig.From(auth);
    private readonly object _gate = new();

    private ResolvedRoleMapping? _view;

    /// <summary>
    /// The mapping and its readers. The store hands back the same instance until a write
    /// reassembles its document, so instance identity is what says "this changed" — a save
    /// rebuilds the catalog once and every request after it reads the rebuilt one.
    /// </summary>
    public ResolvedRoleMapping Current()
    {
        var mapping = InForce();
        lock (_gate)
        {
            if (_view is null || !ReferenceEquals(_view.Mapping, mapping))
                // 2026-08-26-7a51: the name claim is bootstrap, so it is captured once and
                // paired with whatever mapping is in force — a person grant is only ever
                // matched against the claim the pipeline actually named callers by.
                _view = ResolvedRoleMapping.From(mapping, auth.NameClaim);
            return _view;
        }
    }

    private RoleMappingConfig InForce() => stored.Read() ?? _seed;
}
