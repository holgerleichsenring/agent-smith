using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using System.Text.Json;

namespace AgentSmith.Tests.Handlers;

public sealed class AttachmentLoaderTests
{
    [Fact]
    public void JiraAttachmentLoader_ParseRefs_ReturnsImageAttachments()
    {
        var json = """
        {
            "attachment": [
                { "filename": "screenshot.png", "mimeType": "image/png", "content": "https://jira.example.com/attachment/1", "size": 12345 },
                { "filename": "doc.pdf", "mimeType": "application/pdf", "content": "https://jira.example.com/attachment/2", "size": 99999 },
                { "filename": "error.jpg", "mimeType": "image/jpeg", "content": "https://jira.example.com/attachment/3", "size": 54321 }
            ]
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var refs = JiraAttachmentLoader.ParseRefs(doc.RootElement);

        refs.Should().HaveCount(2);
        refs[0].FileName.Should().Be("screenshot.png");
        refs[0].MimeType.Should().Be("image/png");
        refs[1].FileName.Should().Be("error.jpg");
        refs[1].MimeType.Should().Be("image/jpeg");
    }

    [Fact]
    public void JiraAttachmentLoader_ParseRefs_SkipsOversizedImages()
    {
        var json = $$"""
        {
            "attachment": [
                { "filename": "huge.png", "mimeType": "image/png", "content": "https://jira.example.com/attachment/1", "size": {{TicketImageAttachment.MaxSizeBytes + 1}} }
            ]
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var refs = JiraAttachmentLoader.ParseRefs(doc.RootElement);

        refs.Should().BeEmpty();
    }

    [Fact]
    public void JiraAttachmentLoader_ParseRefs_NoAttachments_ReturnsEmpty()
    {
        var json = "{}";
        using var doc = JsonDocument.Parse(json);
        var refs = JiraAttachmentLoader.ParseRefs(doc.RootElement);

        refs.Should().BeEmpty();
    }

    [Fact]
    public void GitHubAttachmentLoader_ParseRefs_ExtractsMarkdownImages()
    {
        var body = """
            Fix the bug shown below:
            ![screenshot](https://user-images.githubusercontent.com/123/screenshot.png)
            Also see this log:
            ![error](https://user-images.githubusercontent.com/123/error.jpg)
            And this link (not an image): [docs](https://example.com/docs)
            """;

        var refs = GitHubAttachmentLoader.ParseRefs(body);

        refs.Should().HaveCount(2);
        refs[0].FileName.Should().Be("screenshot.png");
        refs[0].MimeType.Should().Be("image/png");
        refs[1].FileName.Should().Be("error.jpg");
        refs[1].MimeType.Should().Be("image/jpeg");
    }

    [Fact]
    public void GitHubAttachmentLoader_ParseRefs_NullBody_ReturnsEmpty()
    {
        var refs = GitHubAttachmentLoader.ParseRefs(null);
        refs.Should().BeEmpty();
    }

    [Fact]
    public void GitHubAttachmentLoader_ParseRefs_NoImages_ReturnsEmpty()
    {
        var refs = GitHubAttachmentLoader.ParseRefs("Just text, no images.");
        refs.Should().BeEmpty();
    }

    [Fact]
    public void TicketImageAttachment_IsSupportedImage_AcceptsPngJpgGifWebp()
    {
        TicketImageAttachment.IsSupportedImage("image/png").Should().BeTrue();
        TicketImageAttachment.IsSupportedImage("image/jpeg").Should().BeTrue();
        TicketImageAttachment.IsSupportedImage("image/gif").Should().BeTrue();
        TicketImageAttachment.IsSupportedImage("image/webp").Should().BeTrue();
    }

    [Fact]
    public void TicketImageAttachment_IsSupportedImage_RejectsPdfZip()
    {
        TicketImageAttachment.IsSupportedImage("application/pdf").Should().BeFalse();
        TicketImageAttachment.IsSupportedImage("application/zip").Should().BeFalse();
        TicketImageAttachment.IsSupportedImage("text/plain").Should().BeFalse();
    }

    [Fact]
    public void TicketImageAttachment_IsSupportedImage_RejectsOversized()
    {
        var oversized = new AttachmentRef("url", "big.png", "image/png",
            TicketImageAttachment.MaxSizeBytes + 1);
        TicketImageAttachment.IsSupportedImage(oversized).Should().BeFalse();
    }

    [Fact]
    public void TicketImageAttachment_Base64_EncodesContent()
    {
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic bytes
        var attachment = new TicketImageAttachment(
            new AttachmentRef("url", "test.png", "image/png"), content);

        attachment.Base64.Should().Be(Convert.ToBase64String(content));
        attachment.MediaType.Should().Be("image/png");
    }
}
