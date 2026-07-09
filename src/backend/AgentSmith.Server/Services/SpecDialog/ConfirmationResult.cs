namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315e: how the in-thread confirmation of a proposed outcome ended. Only
/// an explicit approval confirms — anything else keeps the dialogue open and
/// files nothing.
/// </summary>
public enum ConfirmationResult
{
    Confirmed,
    Declined,
    TimedOut,
}
