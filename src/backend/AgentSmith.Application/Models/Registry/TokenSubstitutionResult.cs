namespace AgentSmith.Application.Models.Registry;

/// <summary>
/// Outcome of substituting real registry tokens into a templated auth-config
/// file. On failure (an <c>__AS_TOKEN_&lt;host&gt;__</c> placeholder whose host has
/// no configured registry) <see cref="Content"/> is null and the file MUST NOT
/// be written — a half-substituted or empty auth file silently breaks restore.
/// </summary>
public sealed record TokenSubstitutionResult(bool IsSuccess, string? Content, string? FailureReason)
{
    public static TokenSubstitutionResult Ok(string content) => new(true, content, null);
    public static TokenSubstitutionResult Fail(string reason) => new(false, null, reason);
}
