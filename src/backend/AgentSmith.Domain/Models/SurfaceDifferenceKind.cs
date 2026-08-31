namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-c6ec: the three ways an interface can be wider than the clients that use it.
/// </summary>
public enum SurfaceDifferenceKind
{
    /// <summary>The description offers the operation and no client was found to call it.</summary>
    UnexercisedOperation,

    /// <summary>The operation accepts the property and no client was found to send it.</summary>
    UnsentAcceptedProperty,

    /// <summary>The operation returns the property and no client was found to read it.</summary>
    UnreadReturnedProperty
}
