using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Server.Services.References;
using AgentSmith.Tests.DesignSources;
using AgentSmith.Tests.Persistence.ReferenceFiles;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-08-e8b9j: view_reference_image shows an image inside an upload as a picture — read from
/// the store, on any backend — and the operator is told when the conversation's model cannot see one.
/// </summary>
public sealed class ReferenceImageToolTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x2A];
    private readonly ReferenceSandboxFixture _fixture = new();

    public ReferenceImageToolTests() => _fixture.Set.Add(new ReferenceSetFile("site/shot.png", Png));

    [Fact]
    public async Task ViewReferenceImage_SmallPng_Deposits()
    {
        var deposit = new DesignImageFakes.RecordingDeposit();

        var answer = await Host(deposit).ViewReferenceImage("reference:site", "site/shot.png");

        answer.Should().Be("image: site/shot.png follows this result");
        deposit.Images.Should().ContainSingle().Which.MediaType.Should().Be("image/png");
        deposit.Images[0].Bytes.Should().Equal(Png);
    }

    [Fact]
    public async Task ViewReferenceImage_WorkPrefixedPath_Resolves()
    {
        var deposit = new DesignImageFakes.RecordingDeposit();

        await Host(deposit).ViewReferenceImage("reference:site", "/work/site/shot.png");

        deposit.Images.Should().ContainSingle();
    }

    [Fact]
    public async Task ViewReferenceImage_NotAnImage_SaysSo()
    {
        var answer = await Host(new DesignImageFakes.RecordingDeposit()).ViewReferenceImage("reference:site", "site/notes.txt");

        answer.Should().StartWith("not an image");
    }

    [Fact]
    public async Task ViewReferenceImage_LongEdgeOver1568_SaysUseImageEntry()
    {
        var deposit = new DesignImageFakes.RecordingDeposit("the image is 2880x1800 px; its long edge must be at most 1568 px");

        var answer = await Host(deposit).ViewReferenceImage("reference:site", "site/shot.png");

        answer.Should().Contain("not shown").And.Contain("1568").And.Contain("Image entry");
    }

    [Fact]
    public void DesignTurn_InProcessWithUpload_HasViewReferenceImage()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["reference:site"] = _fixture.Open(Holds.None()) });

        var tools = new ReferenceDesignTools(new SandboxContainerRuntime(false), null, _fixture,
            new DesignImageFakes.RecordingDeposit()).For(pipeline);

        tools.Select(t => t.Name).Should().Contain("view_reference_image").And.NotContain("run_in_reference");
    }

    [Fact]
    public void SpecDialogView_CopilotAgent_SeesUploadedImagesFalse()
    {
        var copilot = Mock.Of<IChatClientBuilder>(b => b.SupportedTypes == new[] { "copilot" } && !b.AcceptsImageAfterToolResult);

        var sight = new DialogImageSight(Loader("copilot", vision: true), [copilot]).For("sample");

        sight.Images.Should().BeTrue("an Image-entry picture rides the user message");
        sight.UploadedImages.Should().BeFalse("a tool result cannot be followed by a picture on this transport");
    }

    [Fact]
    public async Task SpecDialogView_ImageCount_CountsSetImages()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        await new ReferenceSetRepository(db).AddAsync("s-1", [
            new ReferenceFile { RelativePath = "site/a.PNG", MediaType = "image/png", Content = Png },
            new ReferenceFile { RelativePath = "site/b.txt", MediaType = "text/plain", Content = "b"u8.ToArray() },
        ], CancellationToken.None);
        db.Add(new ReferenceFile
        {
            SessionId = "s-1", SetId = "img", Kind = ReferenceFileKind.Image, MediaType = "image/png",
            Content = Png, Length = Png.Length, ContentSha256 = Png.Sha256Hex(),
        });
        await db.SaveChangesAsync();

        var read = await AgentSmith.Tests.TestSupport.TestUploads.Over(db).ReadAsync("s-1", CancellationToken.None);

        read.ImageCount.Should().Be(2);
    }

    private ReferenceImageToolHost Host(DesignImageFakes.RecordingDeposit deposit) =>
        new(new Dictionary<string, ReferenceUploadAddress>
            {
                ["reference:site"] = new(ReferenceSandboxFixture.Conversation, ReferenceSandboxFixture.SetId),
            },
            _fixture, deposit);

    private static IConfigurationLoader Loader(string type, bool vision)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["sample"] = new() { Name = "sample", Agent = new AgentConfig { Type = type, SupportsVision = vision } },
            },
        });
        return loader.Object;
    }
}
