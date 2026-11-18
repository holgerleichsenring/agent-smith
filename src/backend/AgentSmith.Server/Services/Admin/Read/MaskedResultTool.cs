using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>
/// 2026-11-17-f3a8b: an admin tool whose result is masked at the source — every configured credential value is
/// replaced before the text leaves the tool, so a value an event payload, an end detail or a recorded prompt carried
/// never reaches the model or the thread.
/// </summary>
internal sealed class MaskedResultTool(AIFunction inner, SecretMasker masker) : DelegatingAIFunction(inner)
{
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var result = await base.InvokeCoreAsync(arguments, cancellationToken);
        return masker.Apply(result?.ToString() ?? string.Empty, SecretMasker.ProviderPlaceholder);
    }
}
