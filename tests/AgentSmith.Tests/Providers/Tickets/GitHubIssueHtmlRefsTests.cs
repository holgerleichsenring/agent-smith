using System.Net;
using System.Text;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Octokit;
using Octokit.Internal;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-10-25-9dbbd: a private repository's uploads are taken from the rendered body's signed URLs.
/// The HTML below follows GitHub's documented body_html shape for uploads; no private-repository
/// issue was available to record a live one (see decisions/2026-10-25-9dbbd.yaml).
/// </summary>
public sealed class GitHubIssueHtmlRefsTests
{
    private const string Signed =
        "https://private-user-images.githubusercontent.com/1234567/345678901-0a1b2c3d-4e5f-6789-abcd-ef0123456789.png"
        + "?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJnaXRodWIuY29tIn0.c2ln";

    private const string ScreenshotHtml =
        $"""<p dir="auto">Broken layout:</p><p dir="auto"><a target="_blank" rel="noopener noreferrer" href="{Signed}"><img src="{Signed}" alt="screenshot" style="max-width: 100%;"></a></p>""";

    private const string FileHtml =
        """<p dir="auto"><a href="https://github.com/user-attachments/files/17000001/report.pdf">report.pdf</a></p>""";

    private const string CamoHtml =
        """<p dir="auto"><a target="_blank" rel="noopener noreferrer nofollow" href="https://camo.githubusercontent.com/4f1e/68747470733a2f2f"><img src="https://camo.githubusercontent.com/4f1e/68747470733a2f2f" alt="external" data-canonical-src="https://example.com/x.png" style="max-width: 100%;"></a></p>""";

    [Fact]
    public void GitHubIssueHtmlRefs_SignedPrivateImage_ReturnsSignedUrl()
    {
        var refs = new GitHubIssueHtmlRefs().Parse(ScreenshotHtml);

        refs.Should().ContainSingle();
        refs[0].Uri.Should().Be(Signed, "the signed URL carries its own token and is downloaded as given");
        refs[0].FileName.Should().Be("345678901-0a1b2c3d-4e5f-6789-abcd-ef0123456789.png");
        refs[0].MimeType.Should().Be("image/png");
    }

    [Fact]
    public void GitHubIssueHtmlRefs_EncodedAmpersand_Decoded() =>
        new GitHubIssueHtmlRefs().Parse("""<img src="https://private-user-images.githubusercontent.com/1/2.png?a=1&amp;jwt=x">""")
            .Single().Uri.Should().Be("https://private-user-images.githubusercontent.com/1/2.png?a=1&jwt=x");

    [Fact]
    public void GitHubIssueHtmlRefs_CamoImage_Skipped() =>
        new GitHubIssueHtmlRefs().Parse(CamoHtml).Should().BeEmpty();

    [Fact]
    public void GitHubIssueHtmlRefs_UserAttachmentsFile_Returned()
    {
        var refs = new GitHubIssueHtmlRefs().Parse(FileHtml + ScreenshotHtml);

        refs.Should().HaveCount(2);
        refs.Should().Contain(r => r.Uri == "https://github.com/user-attachments/files/17000001/report.pdf"
            && r.FileName == "report.pdf" && r.MimeType == "application/pdf");
    }

    [Fact]
    public void GitHubIssueHtmlRefs_ExternalLink_Skipped() =>
        new GitHubIssueHtmlRefs().Parse("""<a href="https://example.com/files/x.pdf">x</a>""").Should().BeEmpty();

    [Fact]
    public async Task GetAttachmentRefs_BodyHtml_ReadsFullMediaTypeAndSignedUrl()
    {
        var handler = new IssueHandler($$"""{ "number": 4, "body": "![screenshot](https://github.com/user-attachments/assets/0a1b)", "body_html": {{System.Text.Json.JsonSerializer.Serialize(ScreenshotHtml)}} }""");

        var refs = await Refs(handler).ReadAsync(4, CancellationToken.None);

        refs.Single().Uri.Should().Be(Signed);
        handler.Accept.Should().Contain("application/vnd.github.full+json");
        handler.Path.Should().Be("/repos/o/r/issues/4");
    }

    [Fact]
    public async Task GetAttachmentRefs_NoBodyHtml_FallsBackToMarkdown()
    {
        var handler = new IssueHandler("""{ "number": 4, "body": "see ![shot](https://user-images.githubusercontent.com/1/shot.png)" }""");

        var refs = await Refs(handler).ReadAsync(4, CancellationToken.None);

        refs.Single().Uri.Should().Be("https://user-images.githubusercontent.com/1/shot.png");
    }

    // The real Octokit connection over a scripted handler, so the media type and the raw body are Octokit's own.
    private static GitHubIssueAttachmentRefs Refs(HttpMessageHandler handler) =>
        new(new GitHubClient(new Connection(new ProductHeaderValue("t"), GitHubClient.GitHubApiUrl,
            new InMemoryCredentialStore(new Octokit.Credentials("token")), new HttpClientAdapter(() => handler), new SimpleJsonSerializer())), "o", "r");

    private sealed class IssueHandler(string json) : HttpMessageHandler
    {
        public string Accept { get; private set; } = string.Empty;
        public string Path { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Accept = string.Join(",", request.Headers.Accept.Select(a => a.ToString()));
            Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
