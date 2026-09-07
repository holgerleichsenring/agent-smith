using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-07-b7e2: one detection for both readers — the security scan's auditor and the
/// derivation's audit tool — in the order the scan has always applied.
/// </summary>
public sealed class PackageEcosystemDetectorTests
{
    private const string Root = "/work";

    [Theory]
    [InlineData("package-lock.json", PackageEcosystemKind.Npm)]
    [InlineData("package.json", PackageEcosystemKind.Npm)]
    [InlineData("requirements.txt", PackageEcosystemKind.Python)]
    [InlineData("pyproject.toml", PackageEcosystemKind.Python)]
    [InlineData("go.mod", PackageEcosystemKind.Go)]
    public async Task Detect_ARootMarker_NamesItsEcosystem(string marker, PackageEcosystemKind expected)
    {
        var files = new InMemorySandboxFileReader();
        files.Files[$"{Root}/{marker}"] = string.Empty;

        var detected = await new PackageEcosystemDetector().DetectAsync(files, Root, CancellationToken.None);

        detected.Should().BeEquivalentTo(new PackageEcosystem(expected, marker));
    }

    [Fact]
    public async Task Detect_AProjectFileBelowTheRoot_IsDotNet()
    {
        var files = new InMemorySandboxFileReader();
        files.Files[$"{Root}/src/Api/Api.csproj"] = "<Project />";

        var detected = await new PackageEcosystemDetector().DetectAsync(files, Root, CancellationToken.None);

        detected!.Kind.Should().Be(PackageEcosystemKind.DotNet);
        AuditCommands.For(detected)!.Args.Should().Equal("list", "package", "--vulnerable", "--format", "json");
    }

    [Fact]
    public async Task Detect_NoMarker_IsNull()
    {
        var detected = await new PackageEcosystemDetector()
            .DetectAsync(new InMemorySandboxFileReader(), Root, CancellationToken.None);

        detected.Should().BeNull();
    }

    [Fact]
    public void AuditCommands_ARequirementsFile_IsReadByPipAudit()
    {
        var step = AuditCommands.For(new PackageEcosystem(PackageEcosystemKind.Python, "requirements.txt"))!;

        step.Command.Should().Be("pip-audit");
        step.Args.Should().Equal("--format=json", "--requirement=requirements.txt");
        AuditCommands.For(new PackageEcosystem(PackageEcosystemKind.Go, "go.mod")).Should().BeNull();
    }
}
