using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-22-6968: the SCALAR per-project sandbox overrides on both sides of the
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
            HoldSeconds = 600,
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

        // 2026-09-22-6c46: the scalar half, read without the structured block that now
        // hangs under it — that half has its own tests.
        (projected.Sandbox! with { Structured = null }).Should().Be(new ProjectSandbox(
            "mirror.example/dotnet/sdk:9.0", 1800, 600, "mirror.example", "0.1.0-canary", 600));
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
    public void ProjectProjection_ABlockThatOnlyCarriesStructuredOverrides_ProjectsSixNulls()
    {
        var raw = new RawProjectEntry
        {
            Sandbox = new SandboxConfig { Resources = new ResourceLimits { CpuLimit = "4" } },
        };

        var projected = ProjectEntityMapping.ToProject("proj", raw);

        (projected.Sandbox! with { Structured = null }).Should().Be(new ProjectSandbox());
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

    /// <summary>
    /// 2026-09-23-2446: the hold window survived a save until now only because the patch
    /// never mentioned it. The moment the block carried the field the patch began assigning
    /// it, so a projection that did not also READ it would write null over a stored value on
    /// the next save — this pins both halves as one round trip.
    /// </summary>
    [Fact]
    public void ProjectRoundTrip_AStoredHoldWindow_SurvivesASaveThatDidNotChangeIt()
    {
        var existing = Stored();

        // Read the stored project the way the studio does, change something else entirely,
        // and save it back — exactly what an operator editing the agent registry does.
        var projected = ProjectEntityMapping.ToProject("proj", existing);
        var edited = Entity(projected.Sandbox! with { AgentRegistry = "other.example" });
        var patched = RawProjectPatch.Apply(edited, existing);

        patched.Sandbox!.HoldSeconds.Should().Be(600);
        patched.Sandbox.AgentRegistry.Should().Be("other.example");
    }

    [Fact]
    public void ProjectPatch_ASentBlockWithANullHoldWindow_ClearsItToInherit()
    {
        var existing = Stored();

        var patched = RawProjectPatch.Apply(
            Entity(new ProjectSandbox(StepTimeoutSeconds: 1200)), existing);

        // A form that shows the control and sends the block with the field empty is an
        // operator handing the project back to what it inherits.
        patched.Sandbox!.HoldSeconds.Should().BeNull();
    }

    [Fact]
    public void ProjectPatch_AnEntityWithNoSandboxBlock_LeavesTheStoredHoldWindowUntouched()
    {
        var existing = Stored();

        var patched = RawProjectPatch.Apply(Entity(sandbox: null), existing);

        // A client that does not know the block sends none, and nothing it never showed is
        // written — the same absent-means-leave-alone rule the other five already keep.
        patched.Sandbox!.HoldSeconds.Should().Be(600);
    }

    [Fact]
    public void ProjectPatch_AHoldWindowOfZero_IsStoredAsZeroRatherThanAsAbsent()
    {
        var patched = RawProjectPatch.Apply(
            Entity(new ProjectSandbox(HoldSeconds: 0)), existing: null);

        // Zero is a declaration — this project holds nothing — and not the absent marker.
        patched.Sandbox!.HoldSeconds.Should().Be(0);
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
    public void ProjectEntity_ASentBlockNamingOneField_BindsTheOtherFiveAsNull()
    {
        var body = @"{""id"":""p"",""agent"":""a"",""tracker"":""t"",""repos"":[],""pipelines"":[],"
            + @"""sandbox"":{""stepTimeoutSeconds"":60}}";

        var entity = Deserialize(body);

        entity.Sandbox.Should().Be(new ProjectSandbox(StepTimeoutSeconds: 60));
    }

    // ---------------------------------------------------------------------
    // 2026-09-22-6c46: the STRUCTURED three. Their discriminator sits one level down —
    // present means the client renders them, absent means it does not know them at all.
    // ---------------------------------------------------------------------

    [Fact]
    public void ProjectProjection_AProjectWithStructuredOverrides_ProjectsAllThree()
    {
        var projected = ProjectEntityMapping.ToProject("proj", Stored());

        var structured = projected.Sandbox!.Structured!;
        structured.Resources!.CpuLimit.Should().Be("4");
        structured.Images.Should().ContainKey("dotnet").WhoseValue.Should().Be("mirror.example/dotnet:9.0");
        structured.Secrets!.Env.Should().ContainKey("SF_ID").WhoseValue.Should().Be("sf-creds:id");
    }

    [Fact]
    public void ProjectProjection_TheStructuredBlock_CopiesRatherThanAliasesTheStoredOne()
    {
        var stored = Stored();

        var projected = ProjectEntityMapping.ToProject("proj", stored);

        projected.Sandbox!.Structured!.Resources.Should().NotBeSameAs(stored.Sandbox!.Resources);
        projected.Sandbox.Structured.Images.Should().NotBeSameAs(stored.Sandbox.Images);
        projected.Sandbox.Structured.Secrets.Should().NotBeSameAs(stored.Sandbox.Secrets);
    }

    [Fact]
    public void ProjectPatch_ASentBlockWithoutTheStructuredOverrides_LeavesThemUntouched()
    {
        var existing = Stored();

        // A client that renders only the six scalars sends no structured block at all.
        var patched = RawProjectPatch.Apply(
            Entity(new ProjectSandbox(StepTimeoutSeconds: 1200)), existing);

        patched.Sandbox!.Resources!.CpuLimit.Should().Be("4");
        patched.Sandbox.Images.Should().ContainKey("dotnet");
        patched.Sandbox.Secrets!.Env.Should().ContainKey("SF_ID");
    }

    [Fact]
    public void ProjectPatch_ASentStructuredBlockWithNullFields_ClearsThemToInherit()
    {
        var existing = Stored();

        var patched = RawProjectPatch.Apply(
            Entity(new ProjectSandbox(Structured: new ProjectSandboxStructured())), existing);

        patched.Sandbox!.Resources.Should().BeNull();
        patched.Sandbox.Images.Should().BeNull();
        patched.Sandbox.Secrets.Should().BeNull();
    }

    [Fact]
    public void ProjectPatch_AStructuredBlockWithAnEmptyMap_StoresInheritNotAnEmptyMap()
    {
        var existing = Stored();

        var patched = RawProjectPatch.Apply(
            Entity(new ProjectSandbox(Structured: new ProjectSandboxStructured(
                Images: new Dictionary<string, string>(),
                Secrets: new SandboxSecrets()))),
            existing);

        // An empty map is not a declaration: a project cannot say "inherit nothing".
        patched.Sandbox!.Images.Should().BeNull();
        patched.Sandbox.Secrets.Should().BeNull();
    }

    [Fact]
    public void ProjectPatch_ASentStructuredBlock_WritesResourcesImagesAndSecretNames()
    {
        var patched = RawProjectPatch.Apply(
            Entity(new ProjectSandbox(Structured: new ProjectSandboxStructured(
                Resources: new ResourceLimits("500m", "2", "1Gi", "4Gi"),
                Images: new Dictionary<string, string> { ["node"] = "mirror.example/node:20" },
                Secrets: new SandboxSecrets
                {
                    Env = new Dictionary<string, string> { ["SF_ID"] = "sf-creds:id" },
                    Files = [new SandboxSecretFile { Mount = "/secrets/k", Secret = "sf-creds", Key = "jwt" }],
                }))),
            existing: null);

        patched.Sandbox!.Resources!.MemoryLimit.Should().Be("4Gi");
        patched.Sandbox.Images!["node"].Should().Be("mirror.example/node:20");
        patched.Sandbox.Secrets!.Files!.Single().Mount.Should().Be("/secrets/k");
    }

    /// <summary>
    /// The nesting is the discriminator, so the WIRE has to preserve it: a sent sandbox
    /// block with no structured key binds a null structured block, which is what keeps a
    /// scalar-only client from deleting three stored declarations.
    /// </summary>
    [Fact]
    public void ProjectEntity_ASentSandboxBlockWithNoStructuredKey_BindsAnAbsentStructuredBlock()
    {
        var body = @"{""id"":""p"",""agent"":""a"",""tracker"":""t"",""repos"":[],""pipelines"":[],"
            + @"""sandbox"":{""stepTimeoutSeconds"":60}}";

        var entity = Deserialize(body);

        entity.Sandbox!.Structured.Should().BeNull();
    }

    [Fact]
    public void ProjectEntity_ASentStructuredBlockNamingOnlyImages_BindsResourcesAndSecretsAsNull()
    {
        var body = @"{""id"":""p"",""agent"":""a"",""tracker"":""t"",""repos"":[],""pipelines"":[],"
            + @"""sandbox"":{""structured"":{""images"":{""dotnet"":""m/dotnet:9.0""}}}}";

        var entity = Deserialize(body);

        entity.Sandbox!.Structured!.Images!["dotnet"].Should().Be("m/dotnet:9.0");
        entity.Sandbox.Structured.Resources.Should().BeNull();
        entity.Sandbox.Structured.Secrets.Should().BeNull();
    }

    private static ProjectEntity Deserialize(string body) =>
        JsonSerializer.Deserialize<ProjectEntity>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
