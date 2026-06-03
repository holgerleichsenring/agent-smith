using AgentSmith.Application.Services;
using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199d: registers <see cref="StubProjectAnalyzer"/> in place of the
/// production LLM-driven <see cref="ProjectAnalyzer"/>. Used by init-project
/// + autonomous to keep the ScriptedChatClient queue intact for the rounds
/// that actually need it; the canned ProjectMap also gates PublishProject
/// LanguageHandler so BootstrapDispatch matches csharp-bootstrap.
/// </summary>
internal static class HarnessProjectAnalyzerStub
{
    public static void Register(IServiceCollection services)
    {
        services.RemoveAll<IProjectAnalyzer>();
        services.AddSingleton<IProjectAnalyzer, StubProjectAnalyzer>();
    }
}
