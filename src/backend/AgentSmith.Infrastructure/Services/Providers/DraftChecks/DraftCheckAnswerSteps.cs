using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: reads a <see cref="DraftCheckAnswer"/> as the step it answers. Every detail
/// is a status code plus a fixed sentence, or a field parsed out of the JSON.
/// </summary>
public static class DraftCheckAnswerSteps
{
    /// <summary>Any HTTP answer that is not a redirect proves the host; DNS, connect and TLS fail here.</summary>
    public static DraftCheckStep HostStep(this DraftCheckAnswer answer) =>
        answer.Unreached(DraftCheckStep.Host)
        ?? DraftCheckStep.Pass(DraftCheckStep.Host, $"{answer.HostName} answered HTTP {answer.Status}.");

    /// <summary>
    /// 401/403 means the token was refused; a success that is not JSON or names nobody is no
    /// identity either — Azure DevOps answers a bad PAT with a sign-in page.
    /// </summary>
    public static DraftCheckStep IdentityStep(this DraftCheckAnswer answer, Func<JsonElement, string?> name)
    {
        if (answer.Unreached(DraftCheckStep.Identity) is { } unreached) return unreached;
        if (answer.Status is 401 or 403)
            return DraftCheckStep.Fail(DraftCheckStep.Identity, $"HTTP {answer.Status}: the token was not accepted.");
        if (!answer.IsSuccess)
            return DraftCheckStep.Fail(DraftCheckStep.Identity, $"HTTP {answer.Status} for the sign-in check.");
        return answer.Json() is { } json && Field(json, name) is { } who
            ? DraftCheckStep.Pass(DraftCheckStep.Identity, $"Signed in as {who}.")
            : DraftCheckStep.Fail(DraftCheckStep.Identity,
                "The host answered without an identity (not JSON) — a refused token or a sign-in page.");
    }

    public static DraftCheckStep ScopeStep(this DraftCheckAnswer answer, string what)
    {
        if (answer.Unreached(DraftCheckStep.Scope) is { } unreached) return unreached;
        return answer.Status switch
        {
            404 => DraftCheckStep.Fail(DraftCheckStep.Scope, $"{what} was not found, or the token cannot see it (HTTP 404)."),
            401 or 403 => DraftCheckStep.Fail(DraftCheckStep.Scope, $"The token may not read {what} (HTTP {answer.Status})."),
            _ when !answer.IsSuccess => DraftCheckStep.Fail(DraftCheckStep.Scope, $"HTTP {answer.Status} reading {what}."),
            _ when answer.Json() is null => DraftCheckStep.Fail(DraftCheckStep.Scope, $"The answer for {what} was not JSON."),
            _ => DraftCheckStep.Pass(DraftCheckStep.Scope, $"{what} found."),
        };
    }

    /// <summary>
    /// One page, counted by <paramref name="count"/>; <c>more</c> true says "at least N".
    /// </summary>
    public static DraftCheckStep CountStep(
        this DraftCheckAnswer answer, string key, string noun, Func<JsonElement, (int Count, bool More)?> count)
    {
        if (answer.Unreached(key) is { } unreached) return unreached;
        if (!answer.IsSuccess) return DraftCheckStep.Fail(key, $"HTTP {answer.Status} listing {noun}.");
        if (answer.Json() is not { } json || Counted(json, count) is not { } counted)
            return DraftCheckStep.Fail(key, $"The {noun} listing was not the expected JSON.");
        return DraftCheckStep.Pass(key, counted.More ? $"At least {counted.Count} {noun}." : $"{counted.Count} {noun}.");
    }

    private static string? Field(JsonElement json, Func<JsonElement, string?> name)
    {
        try { return name(json) is { Length: > 0 } value ? value : null; }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException) { return null; }
    }

    private static (int Count, bool More)? Counted(JsonElement json, Func<JsonElement, (int Count, bool More)?> count)
    {
        try { return count(json); }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException) { return null; }
    }
}
