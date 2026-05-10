namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Outcome of a guard assertion. <see cref="IsSuccess"/> with null Error means
/// the path is allowed; <see cref="IsSuccess"/> false carries a structured
/// <see cref="GuardError"/> describing the violation.
/// </summary>
public sealed record Result
{
    public required bool IsSuccess { get; init; }
    public GuardError? Error { get; init; }

    public static Result Ok() => new() { IsSuccess = true };

    public static Result Fail(GuardError error) => new() { IsSuccess = false, Error = error };
}
