using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-31-7097: reads the binary a command line names, and only when the command's
/// shape leaves no doubt about it.
/// <para>
/// The same first-token reading elsewhere REJECTS a command, where a wrong read merely
/// runs it anyway. Here it feeds a report about what an image lacks, so it is
/// deliberately conservative: any shell syntax at all (a quote, a variable, a pipe, a
/// chain, a subshell, a redirect) and any first token that is a path, an assignment or a
/// shell word means the command is NOT read. The caller says so instead of guessing.
/// </para>
/// </summary>
public static partial class BareCommandBinary
{
    // A bare name: letters, digits and the punctuation a binary name really uses.
    // '=' (assignment), '/' (path) and '$' (variable) are outside the class by design.
    [GeneratedRegex("^[A-Za-z0-9_.+-]+$")]
    private static partial Regex BareName();

    private static readonly char[] ShellSyntax =
        ['"', '\'', '`', '$', '|', '&', ';', '<', '>', '(', ')', '{', '}', '\\', '\n', '\r'];

    private static readonly char[] TokenSeparators = [' ', '\t'];

    /// <summary>True when the command begins with a bare binary name; false for every
    /// other shape, which the caller records as unprobed.</summary>
    public static bool TryRead(string? command, out string binary)
    {
        binary = string.Empty;
        if (string.IsNullOrWhiteSpace(command)) return false;
        if (command.IndexOfAny(ShellSyntax) >= 0) return false;

        var token = command.Trim().Split(TokenSeparators, 2, StringSplitOptions.RemoveEmptyEntries)[0];
        if (!BareName().IsMatch(token) || ShellReservedWords.Contains(token)) return false;

        binary = token;
        return true;
    }
}
