namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0505: one measured row — what a candidate command DID against one fixture
/// variant, in a pinned toolchain image, with no workspace credentials.
/// <para>
/// Every field except <see cref="Verdict"/> is an observation that nothing in a
/// <c>dotnet test</c> run can re-derive: the toolchain is not installed here.
/// <see cref="Verdict"/> is the one derived field, so it is the one the offline
/// test is allowed to police.
/// </para>
/// </summary>
public sealed record MeasuredCommand(
    string Shape,
    string Variant,
    string Command,
    int ExitCode,
    string Network,
    string Verdict,
    string ToolVersion,
    string Image,
    string FirstLine)
{
    public const string CleanVariant = "clean";
    public const string SyntaxDefect = "yaml-syntax";

    public bool IsRed => ExitCode != 0;
}
