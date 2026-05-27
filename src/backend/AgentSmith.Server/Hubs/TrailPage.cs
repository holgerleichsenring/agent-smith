namespace AgentSmith.Server.Hubs;

/// <summary>
/// p0169h: paginated trail response. Cursor is the Redis stream entry id
/// of the LAST event in this page; pass it to GetTrailPage as <c>fromId</c>
/// for the next batch. <see cref="HasMore"/> indicates whether the stream
/// has events beyond this page within the retained window.
///
/// <para>p0175-fix: Events is <c>IReadOnlyList&lt;object&gt;</c> (not
/// <c>IReadOnlyList&lt;RunEvent&gt;</c>) so System.Text.Json serialises
/// each element using its RUNTIME type — concrete subclass fields
/// (StepIndex, StepName, …) appear on the wire. The previous typed
/// collection caused STJ to use the declared element type (the
/// RunEvent base) and drop every derived field, surfacing as
/// "undefined / NaNs" on the dashboard's Trail tab.</para>
/// </summary>
public sealed record TrailPage(
    string RunId,
    IReadOnlyList<object> Events,
    string? NextCursor,
    bool HasMore);
