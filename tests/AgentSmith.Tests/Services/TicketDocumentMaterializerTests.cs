using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0317: text-like ticket documents land under {RunRecordDir}/attachments/ so
/// the master can read_file them — pdf/docx through in-sandbox markitdown,
/// txt/md as-is; a failed conversion is fail-soft (logged + skipped).
/// </summary>
public sealed class TicketDocumentMaterializerTests
{
    private const string RunRecordDir = ".agentsmith/runs/run-1";

    [Fact]
    public async Task FetchTicket_DocxAttachment_ConvertedIntoRunRecordDir()
    {
        var (factory, writes) = BuildFileReader();
        var sandbox = new StubSandbox(); // exit 0 + one stdout line per run step
        var sut = new TicketDocumentMaterializer(
            factory, NullLogger<TicketDocumentMaterializer>.Instance);
        var docx = new TicketDocumentAttachment(
            new AttachmentRef("https://x/spec.docx", "spec.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            [0x50, 0x4B, 0x03, 0x04]);

        var result = await sut.MaterializeAsync(
            sandbox, RunRecordDir, [docx], CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Path.Should().Be($"{RunRecordDir}/attachments/spec.docx.md");
        result[0].OriginFileName.Should().Be("spec.docx");
        writes.Should().ContainKey($"{RunRecordDir}/attachments/spec.docx.b64");
        writes[$"{RunRecordDir}/attachments/spec.docx.md"].Should().NotBeNullOrWhiteSpace();
        sandbox.RanSteps.Should().Contain(s =>
            s.Args != null && s.Args.Any(a => a.Contains("markitdown")));
    }

    [Fact]
    public async Task MaterializeAsync_TxtAttachment_WrittenAsIsWithoutConversion()
    {
        var (factory, writes) = BuildFileReader();
        var sandbox = new StubSandbox();
        var sut = new TicketDocumentMaterializer(
            factory, NullLogger<TicketDocumentMaterializer>.Instance);
        var txt = new TicketDocumentAttachment(
            new AttachmentRef("https://x/notes.txt", "notes.txt", "text/plain"),
            System.Text.Encoding.UTF8.GetBytes("plain requirement notes"));

        var result = await sut.MaterializeAsync(
            sandbox, RunRecordDir, [txt], CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Path.Should().Be($"{RunRecordDir}/attachments/notes.txt");
        writes[$"{RunRecordDir}/attachments/notes.txt"].Should().Be("plain requirement notes");
        sandbox.RanSteps.Should().BeEmpty("txt needs no in-sandbox conversion");
    }

    [Fact]
    public async Task MaterializeAsync_MarkitdownFails_FailSoftSkipsDocument()
    {
        var (factory, _) = BuildFileReader();
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns((Step step, IProgress<StepEvent>? _, CancellationToken _) =>
                Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 127,
                    TimedOut: false, DurationSeconds: 0.01,
                    ErrorMessage: "markitdown: not found", OutputContent: string.Empty)));
        var sut = new TicketDocumentMaterializer(
            factory, NullLogger<TicketDocumentMaterializer>.Instance);
        var pdf = new TicketDocumentAttachment(
            new AttachmentRef("https://x/spec.pdf", "spec.pdf", "application/pdf"), [1, 2, 3]);

        var result = await sut.MaterializeAsync(
            sandbox.Object, RunRecordDir, [pdf], CancellationToken.None);

        result.Should().BeEmpty("a document that cannot be converted never sinks the run");
    }

    private static (ISandboxFileReaderFactory Factory, Dictionary<string, string> Writes) BuildFileReader()
    {
        var writes = new Dictionary<string, string>(StringComparer.Ordinal);
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.WriteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string path, string content, CancellationToken _) =>
            {
                writes[path] = content;
                return Task.CompletedTask;
            });
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);
        return (factory.Object, writes);
    }
}
