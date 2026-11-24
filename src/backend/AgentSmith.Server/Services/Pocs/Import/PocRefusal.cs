namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: a PoC upload or import refused — the status and the sentence that names why. An empty
/// <see cref="Reason"/> answers an empty 404: what is not the caller's does not exist for them.</summary>
internal sealed record PocRefusal(int Status, string Reason)
{
    public static PocRefusal NotFound { get; } = new(StatusCodes.Status404NotFound, string.Empty);

    public static PocRefusal BadRequest(string reason) => new(StatusCodes.Status400BadRequest, reason);

    public static PocRefusal Conflict(string reason) => new(StatusCodes.Status409Conflict, reason);

    public IResult Answer() => Reason.Length == 0
        ? Results.StatusCode(Status)
        : Results.Text(Reason, "text/plain", statusCode: Status);
}
