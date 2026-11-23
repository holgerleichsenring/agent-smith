using System.Text;
using AgentSmith.Server.Services.Pocs.Import;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: a PoC upload is bounded at pocs.archive.max_zip_mb plus the framing BEFORE its body is
/// read — a declared length over it is refused untouched — and carries one ZIP and the optional target field.</summary>
public sealed class PocUploadBodyTests
{
    [Fact]
    public async Task Read_DeclaredOverTheCeiling_IsRefusedUnread()
    {
        var body = new WatchedStream();
        var http = new DefaultHttpContext();
        http.Request.Body = body;
        http.Request.ContentLength = PocUploadBody.CeilingOf(1) + 1;
        http.Request.ContentType = "multipart/form-data; boundary=x";

        (await Body().ReadAsync(http, 1, CancellationToken.None)).Should().BeNull();
        body.WasRead.Should().BeFalse("a declared length over the ceiling is refused without reading");
    }

    [Fact]
    public async Task Read_OneZipAndATarget_AreTheUpload()
    {
        var http = new DefaultHttpContext();
        http.Request.ContentType = "multipart/form-data; boundary=x";
        http.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["target"] = "main" },
            new FormFileCollection { new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "todo.zip") });

        var upload = await Body().ReadAsync(http, 25, CancellationToken.None);

        upload.Should().NotBeNull();
        (upload!.FileName, upload.Target).Should().Be(("todo.zip", "main"));
        upload.Archive.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void CeilingOf_IsTheBoundPlusOneMegabyteOfFraming() =>
        PocUploadBody.CeilingOf(25).Should().Be(26L * 1024 * 1024);

    private static PocUploadBody Body() => new(NullLogger<PocUploadBody>.Instance);

    private sealed class WatchedStream() : MemoryStream(Encoding.UTF8.GetBytes("--x--"))
    {
        public bool WasRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            WasRead = true;
            return base.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WasRead = true;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
