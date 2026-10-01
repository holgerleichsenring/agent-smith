using System.Buffers.Binary;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.ToolImages;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ToolImages;

/// <summary>
/// 2026-10-01-283dd: the REAL factory chain with scripted providers, one per agent type, and
/// the real deposit over the same ambient frames the factory's tool loop opens.
/// </summary>
internal sealed class ToolImageLoopFixture
{
    private readonly ToolImageLoopFrames _frames = new();
    private readonly List<IChatClientBuilder> _builders = [];

    public ToolImageLoopFixture() =>
        Deposit = new AsyncLocalToolImageDeposit(new ToolImageRule(new ImageDimensionReader()), _frames);

    public IToolImageDeposit Deposit { get; }

    public ToolImageLoopFixture WithProvider(string type, ScriptedToolChat chat, bool acceptsImage = true)
    {
        _builders.Add(new Builder(type, chat, acceptsImage));
        return this;
    }

    public IChatClient Loop(string type, bool vision = true) =>
        Factory().Create(new AgentConfig { Type = type, Model = "m", SupportsVision = vision }, TaskType.Primary);

    public AIFunction DepositingTool(string name, int images) => AIFunctionFactory.Create(() =>
    {
        for (var i = 0; i < images; i++)
            Deposit.Deposit(new ToolImage("image/png", Png(800, 600), $"{name} render {i + 1}"));
        return $"{name} done";
    }, name);

    public static byte[] Png(int width, int height)
    {
        var bytes = new byte[64];
        byte[] header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R'];
        header.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    private ChatClientFactory Factory() =>
        new(
            _builders,
            EventTestStubs.NoOp,
            EventTestStubs.RunContext,
            new ModelPricingResolver(),
            new AgentSmith.Infrastructure.Services.RateLimiting.LlmRateLimiterRegistry(
                NullLogger<AgentSmith.Infrastructure.Services.RateLimiting.LlmRateLimiterRegistry>.Instance),
            new AgentSmith.Infrastructure.Services.RateLimiting.ThrottleWaitReporter(),
            new AgentSmith.Contracts.Runs.NullRunTraceWriter(),
            TurnActivityRecorder.Silent(),
            new CompactionSummaryRequest(),
            new WindowDerivedCompaction(),
            new ToolImageRelay(_frames, new ToolImageMessageComposer()),
            NullLoggerFactory.Instance);

    private sealed class Builder(string type, IChatClient chat, bool acceptsImage) : IChatClientBuilder
    {
        public IReadOnlyList<string> SupportedTypes { get; } = [type];
        public bool AcceptsImageAfterToolResult => acceptsImage;
        public IChatClient Build(AgentConfig agent, ModelAssignment assignment) => chat;
    }
}
