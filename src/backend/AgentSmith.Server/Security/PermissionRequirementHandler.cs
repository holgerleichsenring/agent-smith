using Microsoft.AspNetCore.Authorization;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503b: decides one permission against one endpoint. The fallback policy carries a
/// requirement for EVERY catalogued permission, so this handler runs fourteen times per
/// request and succeeds immediately for the thirteen the endpoint did not ask for — the
/// route's own <see cref="RequiresPermission"/> metadata is what narrows it.
/// <para>
/// It never calls <c>Fail</c>. An explicit failure discards the pending requirements, and
/// those pending requirements ARE the list of missing permissions the refusal reports.
/// </para>
/// </summary>
internal sealed class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!IsDemanded(context, requirement) || Holds(context, requirement))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }

    // Endpoint routing puts the HttpContext in Resource, which is how the declaration
    // p0503a attached to the route reaches a policy that no route file mentions.
    private static bool IsDemanded(AuthorizationHandlerContext context, PermissionRequirement requirement) =>
        (context.Resource as HttpContext)?.GetEndpoint()?.Metadata.GetMetadata<RequiresPermission>()
            ?.Names.Contains(requirement.Permission, StringComparer.Ordinal) == true;

    private static bool Holds(AuthorizationHandlerContext context, PermissionRequirement requirement) =>
        context.User.HasClaim(PermissionClaims.Type, requirement.Permission);
}
