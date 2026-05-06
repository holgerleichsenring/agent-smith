namespace AgentSmith.Domain.Models;

public sealed record FailedTest(string TestName, string? ErrorMessage, string? StackTrace);
