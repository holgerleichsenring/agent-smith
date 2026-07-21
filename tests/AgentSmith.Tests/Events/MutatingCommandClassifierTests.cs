using AgentSmith.Application.Services.Events;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0357: the classifier behind SandboxCommandEvent.IsWrite — WriteFile steps and
/// mutating shell commands count as writes; reads never do. Conservative: false
/// negatives acceptable, false positives not.
/// </summary>
public sealed class MutatingCommandClassifierTests
{
    private readonly MutatingCommandClassifier _sut = new();

    [Theory]
    [InlineData("cat > /tmp/wolverine-api/Program.cs <<'EOF'\nusing System;\nEOF")]
    [InlineData("echo hello >> notes.md")]
    [InlineData("perl -pi -e 's/MediatR/Mediator/g' src/Foo.cs")]
    [InlineData("perl -i.bak -pe 's/a/b/' Foo.cs")]
    [InlineData("sed -i 's/old/new/' appsettings.json")]
    [InlineData("git apply /tmp/fix.patch")]
    [InlineData("patch -p1 < fix.diff")]
    [InlineData("mv old.cs new.cs")]
    [InlineData("cp template.cs Installers/WolverineExtension.cs")]
    [InlineData("mkdir -p src/Installers && touch src/Installers/.keep")]
    [InlineData("dotnet build 2>build.log")]
    [InlineData("some-tool | tee output.txt")]
    public void IsMutating_MutatingShellCommand_True(string shell) =>
        _sut.IsMutating(RunStep(shell)).Should().BeTrue(shell);

    [Theory]
    [InlineData("grep -rn 'AddMediatR' --include='*.cs' .")]
    [InlineData("dotnet build AgentSmith.sln")]
    [InlineData("cat src/Program.cs")]
    [InlineData("ls -la src/")]
    [InlineData("find . -name '*.csproj'")]
    [InlineData("git status")]
    [InlineData("python3 -c \"print('hello')\"")]
    [InlineData("head -n 5 README.md")]
    public void IsMutating_ReadOnlyShellCommand_False(string shell) =>
        _sut.IsMutating(RunStep(shell)).Should().BeFalse(shell);

    [Fact]
    public void IsMutating_WriteFileStep_True() =>
        _sut.IsMutating(new Step(
                Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
                Path: "src/Foo.cs", Content: "x"))
            .Should().BeTrue();

    [Theory]
    [InlineData(StepKind.ReadFile)]
    [InlineData(StepKind.Grep)]
    [InlineData(StepKind.ListFiles)]
    [InlineData(StepKind.DirectoryTree)]
    public void IsMutating_ReadOnlyStepKinds_False(StepKind kind) =>
        _sut.IsMutating(new Step(
                Step.CurrentSchemaVersion, Guid.NewGuid(), kind, Path: "src", Pattern: "x"))
            .Should().BeFalse();

    private static Step RunStep(string shellText) => new(
        Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
        Command: "/bin/sh", Args: ["-c", shellText]);
}
