namespace AgentSmith.Sandbox.Wire;

/// <summary>
/// The wordings a failed step result carries that a CALLER reads back as a meaning.
/// <para>
/// 2026-09-17-042ed: a read of a file that is not there is an absence a reviewer may state;
/// every other failed read is a step that could not run and proves nothing. That distinction
/// was a substring search for "not found" across an assembly boundary, against a sentence two
/// file handlers happened to spell the same way — and any other failure whose text contained
/// those words was laundered into an absence. The producers spell it from here and the reader
/// matches on it, so the two cannot drift apart silently.
/// </para>
/// </summary>
public static class StepErrors
{
    /// <summary>What a file read answers with when the path does not exist; the path follows.</summary>
    public const string FileNotFoundPrefix = "file not found: ";

    /// <summary>The message for a read of <paramref name="path"/>, which is not there.</summary>
    public static string FileNotFound(string path) => FileNotFoundPrefix + path;

    /// <summary>True when a step result's error message says the file is not there — and not
    /// merely that something else went wrong in words that mention it.</summary>
    public static bool IsFileNotFound(string? errorMessage) =>
        errorMessage?.StartsWith(FileNotFoundPrefix, StringComparison.Ordinal) == true;
}
