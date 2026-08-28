using System.Text;
using AgentSmith.Server.Services.Archive;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server.Archive;

/// <summary>
/// 2026-08-28-3793: the two switches an archive request flips for itself — the body ceiling
/// and the response's synchronous writes — and the spool that lands the upload on disk.
/// Each is asserted on the absence case too, because a server that exposes neither feature
/// must still answer rather than throw.
/// </summary>
public sealed class ArchiveRequestLimitTests
{
    [Fact]
    public void Ceiling_ARequestThatCanBeRaised_IsRaisedToTheArchiveLimit()
    {
        var context = new DefaultHttpContext();
        var feature = new WritableBodySizeLimit();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        var raised = new ArchiveUploadCeiling(NullLogger<ArchiveUploadCeiling>.Instance).Raise(context);

        raised.Should().Be(ArchiveUploadCeiling.Bytes);
        feature.MaxRequestBodySize.Should().Be(ArchiveUploadCeiling.Bytes);
        ArchiveUploadCeiling.Bytes.Should().BeGreaterThan(30 * 1024 * 1024,
            "the default ceiling is what this exists to clear");
    }

    [Fact]
    public void Ceiling_ABodyAlreadyBeingRead_RaisesNothingAndSaysSo()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(
            new WritableBodySizeLimit { IsReadOnly = true });

        new ArchiveUploadCeiling(NullLogger<ArchiveUploadCeiling>.Instance).Raise(context)
            .Should().BeNull();
    }

    [Fact]
    public void Ceiling_AServerThatExposesNoSuchFeature_RaisesNothing() =>
        new ArchiveUploadCeiling(NullLogger<ArchiveUploadCeiling>.Instance)
            .Raise(new DefaultHttpContext()).Should().BeNull();

    [Fact]
    public void SynchronousWrites_ARequestThatCanAllowThem_AllowsThem()
    {
        var context = new DefaultHttpContext();
        var feature = new BodyControl();
        context.Features.Set<IHttpBodyControlFeature>(feature);

        new SynchronousResponseWrites(NullLogger<SynchronousResponseWrites>.Instance)
            .Allow(context).Should().BeTrue();

        feature.AllowSynchronousIO.Should().BeTrue();
    }

    [Fact]
    public void SynchronousWrites_AServerThatExposesNoSuchFeature_SaysSo() =>
        new SynchronousResponseWrites(NullLogger<SynchronousResponseWrites>.Instance)
            .Allow(new DefaultHttpContext()).Should().BeFalse();

    [Fact]
    public async Task Spool_AnUploadedBody_BecomesASeekableFileThatIsDeletedOnDispose()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(new WritableBodySizeLimit());
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("not really a zip"));
        var spool = new ArchiveUploadSpool(
            new ArchiveUploadCeiling(NullLogger<ArchiveUploadCeiling>.Instance),
            NullLogger<ArchiveUploadSpool>.Instance);

        string path;
        await using (var spooled = await spool.SpoolAsync(context, CancellationToken.None))
        {
            path = spooled.Name;
            spooled.CanSeek.Should().BeTrue("a zip's directory sits at its end");
            spooled.Position.Should().Be(0);
            new StreamReader(spooled).ReadToEnd().Should().Be("not really a zip");
        }

        File.Exists(path).Should().BeFalse("the temporary file goes with the stream");
    }

    private sealed class WritableBodySizeLimit : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly { get; init; }

        public long? MaxRequestBodySize { get; set; }
    }

    private sealed class BodyControl : IHttpBodyControlFeature
    {
        public bool AllowSynchronousIO { get; set; }
    }
}
