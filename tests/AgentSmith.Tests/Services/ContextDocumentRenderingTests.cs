using AgentSmith.Application.Extensions;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-04-cf3d: how the loaded documents become the one string the prompts bind. One
/// document is verbatim; several are labelled by what tells them apart.
/// </summary>
public sealed class ContextDocumentRenderingTests
{
    [Fact]
    public void RenderLabelled_OneDocument_IsVerbatim()
    {
        IReadOnlyList<ContextDocument> documents = [Doc("primary", "default", ".", "# Rules\n")];

        documents.RenderLabelled().Should().Be("# Rules\n", "a single-context repository keeps its prompt byte-identical");
    }

    [Fact]
    public void RenderLabelled_TwoContextsInOneSandbox_HeadsEachByItsContextAndWorkdir()
    {
        IReadOnlyList<ContextDocument> documents =
            [Doc("primary", "backend", "backend", "# Backend\n"), Doc("primary", "frontend", "frontend", "# Frontend\n")];

        documents.RenderLabelled().Should().Be(
            "## Context: backend (workdir: backend)\n\n# Backend\n\n---\n\n## Context: frontend (workdir: frontend)\n\n# Frontend");
    }

    [Fact]
    public void RenderLabelled_TwoSandboxesOneContextEach_KeepsTheSandboxKeyHeading()
    {
        IReadOnlyList<ContextDocument> documents =
            [Doc("api", "default", ".", "# Api rules"), Doc("web", "default", ".", "# Web rules")];

        documents.RenderLabelled().Should().Be("## api\n\n# Api rules\n\n---\n\n## web\n\n# Web rules",
            "the heading multi-repo runs always had is unchanged");
    }

    [Fact]
    public void RenderLabelled_TwoSandboxesOneHoldingTwoContexts_LabelsBothAxes()
    {
        IReadOnlyList<ContextDocument> documents =
        [
            Doc("api", "backend", "backend", "# Backend"),
            Doc("api", "frontend", "frontend", "# Frontend"),
            new ContextDocument("web", null, null, "/work/.agentsmith/principles.md", "# Flat"),
        ];

        documents.RenderLabelled().Should().Be(
            "## api — Context: backend (workdir: backend)\n\n# Backend\n\n---\n\n"
            + "## api — Context: frontend (workdir: frontend)\n\n# Frontend\n\n---\n\n"
            + "## web\n\n# Flat");
    }

    [Fact]
    public void RenderLabelled_NoDocuments_IsEmpty()
    {
        IReadOnlyList<ContextDocument> documents = [];

        documents.RenderLabelled().Should().BeEmpty();
    }

    private static ContextDocument Doc(string sandboxKey, string context, string workdir, string content) =>
        new(sandboxKey, context, workdir, $"/work/.agentsmith/contexts/{context}/principles.md", content);
}
