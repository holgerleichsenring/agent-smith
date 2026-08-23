using AgentSmith.Server.Security;
using Microsoft.AspNetCore.Authorization;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0503a: how a mapped route states what a caller needs. Written over
/// <see cref="IEndpointConventionBuilder"/> rather than <c>RouteHandlerBuilder</c> so the
/// hub's map call takes the same declaration as a minimal-API route, and generic so the
/// declaration chains onto the existing line — the three files at their file-length
/// ratchet ceiling cannot afford one more.
/// </summary>
internal static class RoutePermissionExtensions
{
    internal static TBuilder Needs<TBuilder>(this TBuilder builder, params string[] permissions)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new RequiresPermission(permissions));
        return builder;
    }

    internal static TBuilder Anonymous<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new AllowAnonymousAttribute(), new AnonymousRoute(reason));
        return builder;
    }
}
