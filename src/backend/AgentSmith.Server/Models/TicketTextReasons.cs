namespace AgentSmith.Server.Models;

/// <summary>Why a ticket could not be read from its tracker — a class, never the exception's text.</summary>
public static class TicketTextReasons
{
    public const string NotFound = "not-found";
    public const string Unauthorized = "unauthorized";
    public const string Unreachable = "unreachable";
    public const string ProjectNotConfigured = "project-not-configured";
}
