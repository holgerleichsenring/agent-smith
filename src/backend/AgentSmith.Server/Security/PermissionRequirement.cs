using Microsoft.AspNetCore.Authorization;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503b: ONE catalogued permission, as a requirement the authorization pipeline can
/// report on individually. The alternative — one combined policy per route — refuses just
/// as correctly and cannot say WHICH permission was missing: an authorization failure
/// carries its failed REQUIREMENTS, so a route that states two permissions needs two of
/// these to name only the one the caller lacked.
/// </summary>
internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
