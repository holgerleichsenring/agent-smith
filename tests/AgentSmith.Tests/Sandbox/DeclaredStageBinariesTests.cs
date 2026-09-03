using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-31-7097: what a declared verify stage contributes to the toolchain probe.
/// The derivation is deliberately incomplete and says so — a command whose shape it
/// cannot read is carried as UNPROBED, never dropped, because a silent partial list is
/// what makes a probe look like a guarantee.
/// </summary>
public sealed class DeclaredStageBinariesTests
{
    [Theory]
    // The repository's own documented idioms — every one of them would be misread by a
    // first-token rule that trusted itself.
    [InlineData("sh -c \"sf org login jwt --client-id $ID\"")]
    [InlineData("VAR=1 dotnet build")]
    [InlineData("cd src && npm ci")]
    [InlineData("(cd web; npm test)")]
    [InlineData("$MAVEN_HOME/bin/mvn -B verify")]
    [InlineData("./gradlew build")]
    [InlineData("npm run lint | tee lint.log")]
    public void Derivation_AShellShapedCommand_IsRecordedAsUnprobed(string command)
    {
        var derived = DeclaredStageBinaries.Derive([Declaring("api", "build", command)]);

        derived.Binaries.Should().BeEmpty("no binary in this command can be named with certainty");
        derived.Unprobed.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new UnprobedStage("api", "build", command));
    }

    [Theory]
    [InlineData("npm ci", "npm")]
    [InlineData("pytest -q", "pytest")]
    [InlineData("mvn -B verify", "mvn")]
    [InlineData("  dotnet   test  ", "dotnet")]
    public void Derivation_ABareCommand_ContributesItsBinary(string command, string binary)
    {
        var derived = DeclaredStageBinaries.Derive([Declaring("api", "test", command)]);

        derived.Unprobed.Should().BeEmpty();
        derived.Binaries.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new DeclaredStageBinary(binary, "api", "test"));
    }

    [Fact]
    public void Derivation_TheSameBinaryTwice_IsAskedAboutOnce()
    {
        var derived = DeclaredStageBinaries.Derive(
        [
            new ContextVerifyStages("api",
                [new ContextYamlVerifyStage("lint", "npm run lint"),
                 new ContextYamlVerifyStage("test", "npm test")]),
        ]);

        derived.Binaries.Should().ContainSingle().Which.StageLabel.Should().Be("lint");
    }

    [Fact]
    public void Derivation_AShellBuiltin_IsNeverProbedAsABinary()
    {
        // No image carries `cd` or `true`, and looking either one up always succeeds —
        // a stage beginning with one names nothing that can be missing.
        var derived = DeclaredStageBinaries.Derive([Declaring("api", "build", "cd src")]);

        derived.Binaries.Should().BeEmpty();
        derived.Unprobed.Should().ContainSingle();
    }

    private static ContextVerifyStages Declaring(string context, string label, string command) =>
        new(context, [new ContextYamlVerifyStage(label, command)]);
}
