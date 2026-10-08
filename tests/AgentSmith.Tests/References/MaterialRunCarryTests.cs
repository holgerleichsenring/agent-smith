using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Tests.Browser;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-08-e8b9k: the images an approval cites reach the run of the filed ticket — written
/// beside the sets, put among the attachments after the ticket's own within the picture ceiling,
/// replaced (not added) on a resume, and a note rather than a failure when they cannot be read.
/// </summary>
public sealed class MaterialRunCarryTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
    private readonly InMemoryFileSandbox _repo = new();
    private readonly Images _images = new();

    [Fact]
    public async Task MaterializeReferenceSets_CitedImage_WrittenAndAttachedAfterTicketImages()
    {
        _images.Held["img-1"] = Png;
        var pipeline = Pipeline(["img-1"], ticketImages: 2);

        var result = await Handler(_images).ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        _repo.Files["/work/.agentsmith/reference/images/img-1.png"].Should().Equal(Png);
        var attached = pipeline.Get<IReadOnlyList<TicketImageAttachment>>(ContextKeys.Attachments);
        attached.Select(a => a.Ref.Uri).Should().Equal("https://t/0.png", "https://t/1.png", "upload:img-1");
        pipeline.Get<IReadOnlyList<CarriedReferenceImage>>(ContextKeys.ReferenceImages).Single().Shown.Should().BeTrue();
    }

    [Fact]
    public async Task MaterializeReferenceSets_RerunOnResume_DoesNotDuplicateUploads()
    {
        _images.Held["img-1"] = Png;
        var pipeline = Pipeline(["img-1"]);

        await Handler(_images).ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);
        await Handler(_images).ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);

        pipeline.Get<IReadOnlyList<TicketImageAttachment>>(ContextKeys.Attachments).Should().ContainSingle();
    }

    [Fact]
    public async Task MaterializeReferenceSets_TenTicketImages_UploadNamedNotAttached()
    {
        _images.Held["img-1"] = Png;
        var pipeline = Pipeline(["img-1"], ticketImages: TicketImagePromptParts.MaxImages);

        await Handler(_images).ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);

        pipeline.Get<IReadOnlyList<TicketImageAttachment>>(ContextKeys.Attachments).Should().HaveCount(TicketImagePromptParts.MaxImages)
            .And.NotContain(a => UploadImageAddress.Is(a));
        var image = pipeline.Get<IReadOnlyList<CarriedReferenceImage>>(ContextKeys.ReferenceImages).Single();
        image.Shown.Should().BeFalse();
        image.Path.Should().Be(".agentsmith/reference/images/img-1.png", "it is still a file the master can copy");
    }

    [Fact]
    public async Task MaterializeReferenceSets_ImagesOnly_IsCited()
    {
        _images.Held["img-1"] = Png;

        var result = await Handler(_images).ExecuteAsync(new MaterializeReferenceSetsContext(Pipeline(["img-1"])), CancellationToken.None);

        result.Message.Should().NotBe(MaterializeReferenceSetsHandler.NoneCited);
        _repo.Files.Should().ContainKey("/work/.git/info/exclude");
    }

    [Fact]
    public async Task MaterializeReferenceSets_NoStore_ImageBecomesNote()
    {
        var pipeline = Pipeline(["img-1"]);

        var result = await Handler(new NoReferenceSetReader()).ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("an image is shown, not built against");
        pipeline.Get<IReadOnlyList<CarriedReferenceImage>>(ContextKeys.ReferenceImages).Single().Path.Should().BeNull();
        ReferencePromptSection.Carried(pipeline, false).Should().Contain("image img-1: could not be read");
    }

    [Fact]
    public async Task UploadAfterApproval_IsNotCarried_UntilAmendment()
    {
        _images.Held["img-1"] = Png;
        _images.Held["img-2"] = Png;

        await Handler(_images).ExecuteAsync(new MaterializeReferenceSetsContext(Pipeline(["img-1"])), CancellationToken.None);

        _repo.Files.Keys.Should().Contain("/work/.agentsmith/reference/images/img-1.png")
            .And.NotContain("/work/.agentsmith/reference/images/img-2.png", "the approval froze what it cites");
    }

    [Fact]
    public void TicketAttachmentPromptSection_UploadsCountedApart()
    {
        var images = new[] { Ticket(0), Upload("img-1"), Upload("img-2") };

        var note = TicketAttachmentPromptSection.Render(images, attached: 3, [], []);

        note.Should().Contain("1 ticket image(s) are attached").And.Contain("2 of the 2 image(s) the approval cites are attached");
    }

    [Fact]
    public void TicketAttachmentPromptSection_NonVision_SaysFilesUnreadable()
    {
        var note = TicketAttachmentPromptSection.Render([Upload("img-1")], attached: 0, [], []);

        note.Should().Contain("cannot see images").And.Contain("read_file cannot read them");
    }

    [Fact]
    public void CodingMaster_CarriedSetBrowserOff_HasViewReferenceImage()
    {
        var fixture = new BrowserRenderFixture();
        var factory = new RenderReferenceToolFactory(fixture.Services(), fixture.CompareServices(),
            new ReferenceDesignTools(new SandboxContainerRuntime(true), null, fixture, fixture));
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets,
            [new CarriedReferenceSet("set-a", "site", "reference:site", "sample-api", ".agentsmith/reference/set-a", "s-1", 2)]);

        factory.Tools(pipeline, isDesignTurn: false).Select(t => t.Name).Should().Equal("view_reference_image");
    }

    private MaterializeReferenceSetsHandler Handler(IReferenceSetReader reader)
    {
        var files = new SandboxFileReaderFactory();
        return new MaterializeReferenceSetsHandler(ApprovedSetDoubles.Resolver(),
            new ReferenceSetCarrier(reader, new ReferenceSetMaterialiser(reader, files, new SandboxBinaryFileWriter()), new ReferenceGitExclusion(files)),
            new ReferenceImageCarrier(reader, new SandboxBinaryFileWriter(), new ReferenceGitExclusion(files)),
            new UploadImageAttachments(), NullLogger<MaterializeReferenceSetsHandler>.Instance);
    }

    private PipelineContext Pipeline(IReadOnlyList<string> images, int ticketImages = 0)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ApprovedSpecSet, SpecApprovalJson.Write(new SpecApprovalRecord("github-7",
            new SpecSet("github-7", [], SpecAccounting.Empty, [], SpecSource.Approved, Approval: new SpecApproval(Noon, "s-1", "person")),
            ["sample-api"], "gh", "sample-api", "7", null, images)));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["sample-api"] = _repo });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, new Dictionary<string, string> { ["sample-api"] = "sample-api" });
        if (ticketImages > 0)
            pipeline.Set<IReadOnlyList<TicketImageAttachment>>(ContextKeys.Attachments, [.. Enumerable.Range(0, ticketImages).Select(Ticket)]);
        return pipeline;
    }

    private static TicketImageAttachment Ticket(int i) => new(new AttachmentRef($"https://t/{i}.png", $"{i}.png", "image/png"), Png);

    private static TicketImageAttachment Upload(string id) => new(new AttachmentRef($"upload:{id}", $"{id}.png", "image/png"), Png);

    private sealed class Images : IReferenceSetReader
    {
        public Dictionary<string, byte[]> Held { get; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(string sessionId, string setId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReferenceSetFile>>([]);

        public Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> ImageSetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([.. Held.Keys]);

        public Task<ReferenceImageFile?> ImageAsync(string sessionId, string setId, CancellationToken cancellationToken) =>
            Task.FromResult(Held.TryGetValue(setId, out var bytes) ? new ReferenceImageFile("image/png", bytes) : null);
    }
}
