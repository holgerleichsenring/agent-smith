using System.Net;
using System.Net.Http;
using System.Text;
using AgentSmith.Application.Services.Tools;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

public sealed class WebToolHostFetchTests
{
    [Fact]
    public async Task Fetch_DefaultMode_ReturnsMarkdownFromHtml()
    {
        const string html = "<html><body><h1>Hello</h1><p>World <a href=\"https://example.test\">link</a></p></body></html>";
        var host = HostWith(html, "text/html");

        var result = await host.WebFetch("https://example.test/", ct: CancellationToken.None);

        result.Should().Contain("# Hello");
        result.Should().Contain("World");
        result.Should().Contain("[link](https://example.test)");
    }

    [Fact]
    public async Task Fetch_RawTrue_ReturnsOriginalBody()
    {
        const string html = "<html><body><h1>Raw</h1></body></html>";
        var host = HostWith(html, "text/html");

        var result = await host.WebFetch("https://example.test/", raw: true, ct: CancellationToken.None);

        result.Should().Contain("<h1>Raw</h1>");
        result.Should().NotContain("# Raw");
    }

    [Fact]
    public async Task Fetch_HonoursMaxLengthAndStartIndex()
    {
        var body = new string('X', 5000);
        var host = HostWith(body, "text/plain");

        var page1 = await host.WebFetch("https://example.test/", max_length: 100, ct: CancellationToken.None);
        var page2 = await host.WebFetch("https://example.test/", max_length: 100, start_index: 100, ct: CancellationToken.None);

        page1.Should().Contain("returned=100");
        page1.Should().Contain("truncated at 100/5000");
        page2.Should().Contain("start_index=100");
        page2.Should().Contain("returned=100");
    }

    [Fact]
    public async Task Fetch_NonHtmlContentType_ReturnsBodyUnchanged()
    {
        const string body = "{ \"key\": \"value\" }";
        var host = HostWith(body, "application/json");

        var result = await host.WebFetch("https://example.test/", ct: CancellationToken.None);

        result.Should().Contain("{ \"key\": \"value\" }");
    }

    [Fact]
    public async Task Fetch_RejectsNonHttpUrls()
    {
        var host = HostWith("ignored", "text/plain");

        var fileResult = await host.WebFetch("file:///etc/passwd", ct: CancellationToken.None);
        var emptyResult = await host.WebFetch("", ct: CancellationToken.None);

        fileResult.Should().StartWith("Error:");
        emptyResult.Should().StartWith("Error:");
    }

    private static WebToolHost HostWith(string body, string contentType)
    {
        var handler = new StubHandler(body, contentType);
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        return new WebToolHost(client);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly string _contentType;

        public StubHandler(string body, string contentType)
        {
            _body = body;
            _contentType = contentType;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType.Split(';')[0])
            {
                CharSet = "utf-8"
            };
            return Task.FromResult(response);
        }
    }
}
