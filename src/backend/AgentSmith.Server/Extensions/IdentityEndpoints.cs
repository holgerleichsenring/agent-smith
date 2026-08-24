using AgentSmith.Server.Security;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0503d: what your token carried, and what this installation made of it. The page an
/// operator needs BEFORE any mapping exists — it names the claim that was looked in, shows
/// the values that arrived, and lists the roles and permissions they resolved to, which is
/// exactly what a first login has to see to write the mapping that makes the next one work.
/// <para>
/// It states <c>identity.read</c> like every other route, so the route table stays the
/// permission table. What it does NOT do is refuse a caller who holds no roles: every
/// authenticated caller holds that one permission, so the policy on this route amounts to
/// an authenticated principal. A page that refused the caller it exists for would answer
/// 403 naming a permission out of a catalog they have no way to read.
/// </para>
/// </summary>
internal static class IdentityEndpoints
{
    internal static WebApplication MapIdentityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/identity", (HttpContext ctx, CallerIdentityResolver identities) =>
            Results.Ok(identities.Resolve(ctx.User)))
           .Needs(Permissions.IdentityRead);
        return app;
    }
}
