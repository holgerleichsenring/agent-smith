using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Authenticates configured personas before triage. Stores bearer tokens
/// in PipelineContext and sets ActiveMode=true when at least one persona
/// successfully authenticates.
/// </summary>
public sealed class SessionSetupHandler(
    ISessionProvider sessionProvider,
    ILogger<SessionSetupHandler> logger)
    : ICommandHandler<SessionSetupContext>
{
    public async Task<CommandResult> ExecuteAsync(
        SessionSetupContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<Dictionary<string, PersonaCredentials>>(
                ContextKeys.Personas, out var credentials) || credentials is null || credentials.Count == 0)
        {
            logger.LogInformation("No personas configured — running in passive mode");
            context.Pipeline.Set(ContextKeys.ActiveMode, false);
            return CommandResult.Ok("Passive mode — no credentials configured");
        }

        context.Pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);
        context.Pipeline.TryGet<string>(ContextKeys.ApiTarget, out var target);

        if (spec is null || string.IsNullOrWhiteSpace(target))
        {
            logger.LogWarning("Swagger spec or API target missing — falling back to passive mode");
            context.Pipeline.Set(ContextKeys.ActiveMode, false);
            return CommandResult.Ok("Passive mode — swagger spec or target missing");
        }

        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var failedPersonas = new List<string>();

        foreach (var (personaName, creds) in credentials)
        {
            try
            {
                var token = await sessionProvider.AuthenticateAsync(
                    target, spec, creds, cancellationToken);

                if (token is not null)
                {
                    tokens[personaName] = token;
                    logger.LogInformation("Persona '{Persona}' authenticated successfully", personaName);
                }
                else
                {
                    failedPersonas.Add(personaName);
                    logger.LogWarning("Persona '{Persona}' authentication returned null token", personaName);
                }
            }
            catch (Exception ex)
            {
                failedPersonas.Add(personaName);
                logger.LogWarning(ex, "Persona '{Persona}' authentication failed", personaName);
            }
        }

        var activeMode = tokens.Count > 0;
        context.Pipeline.Set(ContextKeys.ActiveMode, activeMode);
        context.Pipeline.Set(ContextKeys.Personas, tokens as object);

        var mode = activeMode
            ? $"Active mode — {tokens.Count} persona(s) authenticated"
            : "Passive mode — no personas authenticated";

        if (failedPersonas.Count > 0)
            mode += $" (failed: {string.Join(", ", failedPersonas)})";

        logger.LogInformation("{Mode}", mode);
        return CommandResult.Ok(mode);
    }
}
