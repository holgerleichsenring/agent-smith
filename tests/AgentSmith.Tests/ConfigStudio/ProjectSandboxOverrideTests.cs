using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-22-6968: the five SCALAR per-project sandbox overrides on both sides of the
/// studio's wire. The projection has to tell "this project says nothing" from "this
/// project pins a value", and the patch has to leave alone what it was not sent — the
/// stored block survived a save until now only because nothing ever mentioned it.
/// </summary>
public sealed class ProjectSandboxOverrideTests
{
    private static RawProjectEntry Stored() => new()
    {
        Agent = "gpt5",
        Tracker = "azdo",
        Sandbox = new SandboxConfig
        {
            ToolchainImage = "mirror.example/dotnet/sdk:9.0",
            StepTimeoutSeconds = 1800,
            RunCommandTimeoutSeconds = 600,
            AgentRegistry = "mirror.example",
            AgentVersion = "0.1.0-canary",
            Resources = new ResourceLimits { CpuLimit = "4", MemoryLimit = "8Gi" },
            Images = new Dictionary<string, string> { ["dotnet"] = "mirror.example/dotnet:9.0" },
            Secrets = new SandboxSecrets { Env = new Dictionary<string, string> { ["SF_ID"] = "sf-creds:id" } },
        },
    };

    private static ProjectEntity Entity(ProjectSandbox? sandbox) =>
        new("proj", "gpt5", "azdo", ["api"], null, [], Sandbox: sandbox);

    [Fact]
    public void ProjectProjection_AProjectWithScalarSandboxOverrides_ProjectsThemAll()
    {
        var projected = ProjectEntityMapping.ToProject("proj", Stored());

        projected.Sandbox.Should().Be(new ProjectSandbox(
            "mirror.example/dotnet/sdk:9.0", 1800, 600, "mirror.example", "0.1.0-canary"));
    }

    [Fact]
    public void ProjectProjection_AProjectWithNoSandboxBlock_ProjectsAbsenceNotDefaults()
    {
        var raw = new RawProjectEntry { Agent = "gpt5", Tracker = "azdo" };

        var projected = ProjectEntityMapping.ToProject("proj", raw);

        // Absence, not a copy of the process-wide 900/300 — a control whose placeholder
        // came from a fabricated block could not say "this project overrides nothing".
        projected.Sandbox.Should().BeNull();
    }

    [Fact]
    public void ProjectProjection_ABlockThatOnlyCarriesStructuredOverrides_ProjectsFiveNulls()
    {
        var raw = new RawProjectEntry
        {
            Sandbox = new SandboxConfig { Resources = new ResourceLimits { CpuLimit = "4" } },
        };

        var projected = ProjectEntityMapping.ToProject("proj", raw);

        projected.Sandbox.Should().Be(new ProjectSandbox());
    }

    [Fact]
    public void ProjectPatch_AnEntityWithNoSandboxBlock_LeavesTheStoredBlockUntouched()
    {
        var existing = Stored();

        var patched = RawProjectPatch.Apply(Entity(sandbox: null), existing);

        patched.Sandbox!.ToolchainImage.Should().Be("mirror.example/dotnet/sdk:9.0");
        patched.Sandbox.StepTimeoutSeconds.Should().Be(1800);
        patched.Sandbox.RunCommandTimeoutSeconds.Should().Be(600);
        patched.Sandbox.AgentRegistry.Should().Be("mirror.example");
        patched.Sandbox.AgentVersion.Should().Be("0.1.0-canary");
    }

    [Fact]
    public void ProjectPatch_ASentBlockWithANullField_ClearsThatFieldToInherit()
    {
        var existing = Stored();

        var patched = RawProjectPatch.Apply(
            Entity(new ProjectSandbox(StepTimeoutSeconds: 1200)), existing);

        patched.Sandbox!.StepTimeoutSeconds.Should().Be(1200);
        // Everything else the sent block carried as null is CLEARED — that is how the
        // form hands a control back to what it would inherit.
        patched.Sandbox.ToolchainImage.Should().BeNull();
        patched.Sandbox.RunCommandTimeoutSeconds.Should().BeNull();
        patched.Sandbox.AgentRegistry.Should().BeNull();
        patched.Sandbox.AgentVersion.Should().BeNull();
    }

    [Fact]
    public void ProjectPatch_ASentBlock_LeavesTheStructuredOverridesItDoesNotCarryUntouched()
    {
        var existing = Stored();

        var patched = RawProjectPatch.Apply(
            Entity(new ProjectSandbox(AgentRegistry: "other.example")), existing);

        patched.Sandbox!.Resources!.CpuLimit.Should().Be("4");
        patched.Sandbox.Images.Should().ContainKey("dotnet");
        patched.Sandbox.Secrets!.Env.Should().ContainKey("SF_ID");
    }

    [Fact]
    public void ProjectPatch_ASentBlockOnAProjectThatHadNone_CreatesTheBlock()
    {
        var patched = RawProjectPatch.Apply(
            Entity(new ProjectSandbox(StepTimeoutSeconds: 60)), existing: null);

        patched.Sandbox!.StepTimeoutSeconds.Should().Be(60);
    }

    /// <summary>
    /// The discriminator only works if the WIRE preserves it: a body that omits the block
    /// has to bind null, and a sent block has to bind its unmentioned fields null. The API
    /// binds unmapped members by skipping them, so this is where the rule actually lives.
    /// </summary>
    [Fact]
    public void ProjectEntity_ABodyWithNoSandboxKey_BindsAnAbsentBlock()
    {
        var body = @"{""id"":""p"",""agent"":""a"",""tracker"":""t"",""repos"":[],""pipelines"":[]}";

        var entity = Deserialize(body);

        entity.Sandbox.Should().BeNull();
    }

    [Fact]
    public void ProjectEntity_ASentBlockNamingOneField_BindsTheOtherFourAsNull()
    {
        var body = @"{""id"":""p"",""agent"":""a"",""tracker"":""t"",""repos"":[],""pipelines"":[],"
            + @"""sandbox"":{""stepTimeoutSeconds"":60}}";

        var entity = Deserialize(body);

        entity.Sandbox.Should().Be(new ProjectSandbox(StepTimeoutSeconds: 60));
    }

    private static ProjectEntity Deserialize(string body) =>
        JsonSerializer.Deserialize<ProjectEntity>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
