using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-25-54bea: the coding master is told which context of which template it follows, at
/// which revision, and reads that context's principles and limits as advice for NEW units.
/// </summary>
public sealed class MasterTemplateAdviceTests
{
    private const string Address = "template:default";
    private const string ContextPrinciples = "/work/.agentsmith/contexts/server/principles.md";
    private const string RootPrinciples = "/work/.agentsmith/principles.md";

    [Fact]
    public void TemplatePromptSection_NamesContextPathAndRevision()
    {
        var declared = CodingMasterTemplateTests.ProjectWithTemplate().Templates[0];

        var section = TemplatePromptSection.Build(
            [Address], new Dictionary<string, (ProjectTemplate, string?)> { [Address] = (declared, "abc123") });

        section.Should().Contain($"`{Address}` — the `server` context of `reference-project` at `abc123`")
            .And.Contain("`.agentsmith/contexts/server/`")
            .And.Contain("this project's `default` context");
    }

    [Fact]
    public void TemplatePromptSection_AddressOnly_KeepsTheBareBullet() =>
        TemplatePromptSection.Build([Address]).Should().Contain($"- `{Address}`\n");

    [Fact]
    public async Task TemplateOverlay_PrinciplesOfTemplateContext_Rendered()
    {
        var files = new InMemorySandboxFileReader();
        files.Files[ContextPrinciples] = "# Server rules\nOne handler per command.\n\n"
            + "## Limits\n```yaml\nfile_lines: 150\n```\n";
        files.Files[RootPrinciples] = "# Root rules\n";

        var section = await AttachedSectionAsync(files);

        section.Should().Contain($"What template `{Address}` advises")
            .And.Contain("from `.agentsmith/contexts/server/principles.md`")
            .And.Contain("One handler per command.")
            .And.Contain("NEW units").And.Contain("never a gate")
            .And.Contain("its Limits fence): file_lines 150")
            .And.Contain("How the template proves itself: read .agentsmith/contexts/server/context.yaml at abc123");
        section.Should().NotContain("# Root rules", "the declared context's own principles win");
    }

    [Fact]
    public async Task TemplateOverlay_NoContextPrinciples_FallsBackToRootPrinciples()
    {
        var files = new InMemorySandboxFileReader();
        files.Files[RootPrinciples] = "# Root rules\nKeep it small.\n";

        var section = await AttachedSectionAsync(files);

        section.Should().Contain("from `.agentsmith/principles.md`").And.Contain("Keep it small.");
    }

    [Fact]
    public async Task TemplateOverlay_NoPrinciplesFile_SectionSaysSo()
    {
        var section = await AttachedSectionAsync(new InMemorySandboxFileReader());

        section.Should().Contain("carries no principles file")
            .And.Contain("`.agentsmith/contexts/server/principles.md`")
            .And.NotContain("Limits fence");
    }

    [Fact]
    public void Attachment_Empty_KeepsTheAddressOnlySection() =>
        MasterTemplateAttachment.Empty.Section([Address])
            .Should().Contain($"- `{Address}`\n").And.NotContain("advises");

    private static async Task<string> AttachedSectionAsync(InMemorySandboxFileReader files)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            { ["Sample.Server"] = new Mock<ISandbox>().Object });
        pipeline.Set(ContextKeys.ProjectConfig, CodingMasterTemplateTests.ProjectWithTemplate());
        var draft = new SpecDraft("2026-10-25-54bea", "goal", "yaml", []) { Contexts = ["default"] };

        await using var attachment = await CodingMasterTemplateTests
            .Scopes(new CodingMasterTemplateTests.RecordingScope(), new DerivationTestLooks.FixedReaderFactory(files))
            .OpenAsync(pipeline, draft, readOnlySurface: false, CancellationToken.None);
        return attachment.Section(attachment.Names);
    }
}
