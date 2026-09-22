using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// The resume feature-set's bindings, held next to the services they name
/// rather than in the pipeline-execution registration list: p0327's durable
/// dialogue (ask gate, checkpoint writer, context (de)serializer, resume
/// reader, queue-riding resumer, and a508's one identity) and p0356's
/// same-ticket resume seed.
/// <para>The DB-free defaults are TryAdd: the server's relational composition
/// registers its own before this runs and keeps them.</para>
/// </summary>
public static class ResumeRegistrations
{
    public static IServiceCollection AddResumeServices(this IServiceCollection services)
    {
        services.AddTransient<IDialogueJobIdentity, DialogueJobIdentity>();
        services.AddTransient<IPipelineContextSerializer, PipelineContextSerializer>();
        services.AddTransient<IDialogueCheckpointWriter, DialogueCheckpointWriter>();
        services.AddTransient<IDialogueAskGate, DialogueAskGate>();
        services.AddTransient<ResumeRequestReader>();
        services.AddTransient<ResumedCapRecompute>();
        services.AddTransient<IRunResumer, RunResumer>();
        services.AddTransient<PriorRunSeedSource>();
        services.TryAddSingleton<IRunCheckpointStore, NoOpRunCheckpointStore>();
        services.TryAddSingleton<IDialogueAnswerInbox, NoOpDialogueAnswerInbox>();
        services.TryAddSingleton<IPriorRunLedgerReader, NullPriorRunLedgerReader>();
        return services;
    }
}
