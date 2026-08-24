namespace AgentSmith.Server.Security;

/// <summary>
/// p0503a: the permission a route needs, carried as OUR metadata and inspected by
/// nothing the framework runs. Deliberately not <c>RequireAuthorization</c>: routing's
/// EndpointMiddleware throws "contains authorization metadata, but a middleware was not
/// found that supports authorization" when an executed endpoint carries
/// <c>IAuthorizeData</c> and no authorization middleware ran, and this server registers
/// none — so the framework's own declaration would turn sixty-three routes into 500s.
/// <para>
/// A route may state SEVERAL permissions, and they are required TOGETHER: the four
/// routes that cross the config/secrets boundary state both, so a holder of
/// <c>config.*</c> alone can neither read a secret's name out of the change feed nor
/// remove one through revert. Translating this into a policy is the job of *a token is
/// validated against one authority*, at the one moment a pipeline exists.
/// </para>
/// </summary>
internal sealed record RequiresPermission
{
    internal RequiresPermission(params string[] names)
    {
        if (names.Length == 0)
            throw new ArgumentException("A route states at least one permission.", nameof(names));
        Names = names;
    }

    internal IReadOnlyList<string> Names { get; }
}
