using AgentSmith.Application.Services.Handlers;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0451: a command that cannot fail is not a verification.
/// <para>
/// Live run 587c. The analyzer wrote <c>echo Build command placeholder</c> as the
/// BackgroundWorker's declared build stage. Verification executed it, it exited 0, and the
/// mechanical gate reported "sample…BackgroundWorker [build+test] green" over a repository
/// nothing had compiled. The delivery account was the only thing that noticed — "the
/// BackgroundWorker build command was a placeholder and does not demonstrate a migrated
/// solution build" — and it was right while the gate was wrong.
/// </para>
/// <para>
/// A declared stage that cannot fail is worth exactly as much as no declared stage: the
/// resolver falls through to discovery, and if that finds nothing the run says so instead
/// of claiming a build it never ran.
/// </para>
/// </summary>
public sealed class VerificationCommandTests
{
    [Theory]
    [InlineData("echo Build command placeholder")]
    [InlineData("  echo   ok  ")]
    [InlineData("true")]
    [InlineData(":")]
    [InlineData("# build here")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ACommandThatCannotFail_IsNotAVerification(string? command)
        => VerificationCommand.CanFail(command).Should().BeFalse();

    [Theory]
    [InlineData("dotnet build")]
    [InlineData("dotnet test sample.Sample.Tests")]
    [InlineData("npm run build")]
    [InlineData("make check")]
    [InlineData("./gradlew assemble")]
    public void ARealToolchainCommand_CanFail(string command)
        => VerificationCommand.CanFail(command).Should().BeTrue();

    /// <summary>
    /// The no-op check reads the command, not the words inside it: a real build whose
    /// arguments merely mention echoing is still a real build.
    /// </summary>
    [Fact]
    public void ARealCommandMentioningANoOp_IsStillAVerification()
        => VerificationCommand.CanFail("dotnet build -p:Message=echo").Should().BeTrue();
}
