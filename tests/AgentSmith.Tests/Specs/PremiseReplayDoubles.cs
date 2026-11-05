using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Structure;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-11-04-6768a: the series-base replay as the product composes it, every part real.</summary>
internal static class PremiseReplayDoubles
{
    public static SeriesBase SeriesBase() => new(
        new SandboxBaseLadder(new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance),
            NullLogger<SandboxBaseLadder>.Instance),
        new StructureGitRunner(), NullLogger<SeriesBase>.Instance);

    public static PremiseBaseReplay Replay() => new(
        new SandboxTargets(), SeriesBase(), new PremiseLookFlip(new StructureGitRunner()),
        new StructureGitRunner(), NullLogger<PremiseBaseReplay>.Instance);

    public static PremiseSupersessionFilter Filter(ISpecProgressRecorder progress) =>
        new(Replay(), progress, NullLogger<PremiseSupersessionFilter>.Instance);
}
