using AgentSmith.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503b: turns a refusal into an answer. Everything except a permission shortfall — a
/// challenge for a caller with no token, an explicit failure — is left to the framework's
/// own handler, so this adds a body exactly where the framework has none to add.
/// </summary>
internal sealed class PermissionAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _framework = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context,
        AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var missing = Missing(authorizeResult);
        if (missing.Count == 0)
        {
            await _framework.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ForbiddenPermissionResponse(
            "The caller is missing one or more permissions this route requires.", missing));
    }

    // FailedRequirements is populated only for a failure nothing called Fail() on — which
    // is why PermissionRequirementHandler leaves a missing permission PENDING.
    private static IReadOnlyList<string> Missing(PolicyAuthorizationResult result) =>
        result.Forbidden && result.AuthorizationFailure is { } failure
            ? [.. failure.FailedRequirements.OfType<PermissionRequirement>()
                .Select(r => r.Permission).Order(StringComparer.Ordinal)]
            : [];
}
