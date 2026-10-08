namespace AgentSmith.Server.Services.References;

/// <summary>2026-10-08-e8b9g: why an upload may not join its conversation, and the status that says so.</summary>
public sealed record UploadRefusal(int Status, string Reason)
{
    /// <summary>Past the conversation's byte cap: a request the caller can change.</summary>
    public static UploadRefusal OverCap(string reason) => new(StatusCodes.Status400BadRequest, reason);

    /// <summary>A copy of an upload the conversation holds.</summary>
    public static UploadRefusal Duplicate(string reason) => new(StatusCodes.Status409Conflict, reason);

    /// <summary>The answer the route returns.</summary>
    public IResult Answer() =>
        Status == StatusCodes.Status409Conflict ? Results.Conflict(Reason) : Results.BadRequest(Reason);
}
