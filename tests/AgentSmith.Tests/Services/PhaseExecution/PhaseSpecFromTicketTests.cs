using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using Markdig;

namespace AgentSmith.Tests.Services.PhaseExecution;

/// <summary>
/// p0315d: PhaseSpecFromTicket inverts the phase-ticket shape — the single fenced ```yaml
/// block in a phase ticket body comes back as a schema-validated PhaseDraft, byte-identical
/// spec. The AzDO variant (body stored markdown→HTML, fence HTML-encoded in pre/code)
/// round-trips too. Validation is the PRODUCTION SpecDraftValidator, not a test regex.
/// <para>
/// 2026-09-17-0e79a: the body under test is HAND-WRITTEN. The framework files a requirement and
/// stores the approved set instead, so what the extractor still inverts is a phase ticket a
/// person wrote — prose plus exactly one fenced block, and nothing else opening one.
/// </para>
/// </summary>
public sealed class PhaseSpecFromTicketTests
{
    private const string ValidYaml =
        """
        phase: p9999
        goal: "Add a widget endpoint to the sample service"
        requires:
          - p9998
        steps:
          - id: impl
            action: "Add the widget endpoint + handler"
        tests:
          - "Widget_Get_ReturnsWidget"
        done:
          - "GET /widget returns the widget"
        """;

    private readonly PhaseSpecFromTicket _sut = new(
        new SpecDraftValidator(new PhaseSpecSchemaProvider()), new PhaseDraftReader());

    // What a person writes: a summary, then the single fenced block, nothing else fencing.
    private static readonly string HandWrittenBody =
        "## Goal\nAdd a widget endpoint to the sample service\n\n---\n\n```yaml\n"
        + ValidYaml.Trim() + "\n```\n";

    [Fact]
    public void PhaseSpecFromTicket_ExtractsAndValidatesYamlBlock()
    {
        var body = HandWrittenBody;

        var extraction = _sut.Extract(body);

        var extracted = extraction.Should().BeOfType<PhaseSpecExtracted>().Subject;
        extracted.Draft.PhaseId.Should().Be("p9999");
        extracted.Draft.Goal.Should().Be("Add a widget endpoint to the sample service");
        extracted.Draft.Requires.Should().BeEquivalentTo(["p9998"]);
        extracted.Draft.Yaml.Should().Be(ValidYaml.Trim(),
            "the spec must come back verbatim — the renderer files it byte-identical");
    }

    [Fact]
    public void Extract_AzureDevOpsHtmlEncodedBody_DecodesAndValidates()
    {
        // AzDO stores System.Description as HTML: the create path converts the
        // markdown body with the SAME Markdig pipeline the provider uses, so the
        // fence reads back as <pre><code class="language-yaml"> with entities.
        var markdown = HandWrittenBody;
        var html = Markdown.ToHtml(markdown, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
        html.Should().NotContain("```yaml", "the HTML variant must exercise the pre/code path");

        var extraction = _sut.Extract(html);

        var extracted = extraction.Should().BeOfType<PhaseSpecExtracted>().Subject;
        extracted.Draft.PhaseId.Should().Be("p9999");
        extracted.Draft.Yaml.Should().Be(ValidYaml.Trim(),
            "HTML entity decoding must restore the exact yaml the renderer filed");
    }

    [Fact]
    public void Extract_BodyWithoutYamlBlock_ReturnsInvalidWithReason()
    {
        var extraction = _sut.Extract("## Goal\nJust prose, no spec block.");

        extraction.Should().BeOfType<PhaseSpecInvalid>()
            .Which.Error.Should().Contain("no fenced ```yaml block");
    }

    [Fact]
    public void Extract_SchemaInvalidYaml_ReturnsInvalidWithValidatorError()
    {
        var body = "```yaml\ngoal: \"missing the phase id\"\n```";

        var extraction = _sut.Extract(body);

        extraction.Should().BeOfType<PhaseSpecInvalid>()
            .Which.Error.Should().NotBeNullOrWhiteSpace();
    }
}
