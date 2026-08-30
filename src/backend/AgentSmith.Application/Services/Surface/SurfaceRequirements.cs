using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: the entry of the shipped verification standard that DECIDES whether a
/// difference matters. The difference is evidence; the requirement is the finding.
/// <para>
/// An operation nobody calls is untidy until function-level access to it turns out to be
/// unrestricted; a property accepted and never sent is untidy until the operation turns
/// out not to limit the fields it binds; a property returned and never read is untidy
/// until the response turns out to carry more of a data object than it owes. Each id is
/// checked against the checked-in catalogue by the suite, and means nothing without the
/// catalogue version the report carries.
/// </para>
/// </summary>
public static class SurfaceRequirements
{
    /// <summary>Function-level access is restricted to consumers with explicit permissions.</summary>
    public const string UnexercisedOperation = "V8.2.1";

    /// <summary>Allowed fields are limited per controller and action (mass assignment).</summary>
    public const string UnsentAcceptedProperty = "V15.3.3";

    /// <summary>Only the required subset of a data object's fields is returned.</summary>
    public const string UnreadReturnedProperty = "V15.3.1";

    public static string For(SurfaceDifferenceKind kind) => kind switch
    {
        SurfaceDifferenceKind.UnexercisedOperation => UnexercisedOperation,
        SurfaceDifferenceKind.UnsentAcceptedProperty => UnsentAcceptedProperty,
        SurfaceDifferenceKind.UnreadReturnedProperty => UnreadReturnedProperty,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
