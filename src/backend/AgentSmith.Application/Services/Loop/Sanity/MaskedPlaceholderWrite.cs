using System.Collections;
using System.Text.Json;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// 2026-10-25-c5a7i: a write that would put the provider's secret placeholder into a file. The
/// model only ever sees <see cref="SecretMasker.ProviderPlaceholder"/> where a secret was, so
/// write_file, edit or multi_edit carrying it would replace real bytes with the placeholder. Any
/// string argument at any depth counts (multi_edit's new text sits inside its edits array). The
/// refusal is the tool's "Error: …" result and the inner tool is never invoked.
/// </summary>
public sealed class MaskedPlaceholderWrite(AIFunction inner) : DelegatingAIFunction(inner)
{
    private static readonly HashSet<string> WriteTools = new(StringComparer.Ordinal) { "write_file", "edit", "multi_edit" };

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!WriteTools.Contains(Name) || !arguments.Values.Any(Carries))
            return base.InvokeCoreAsync(arguments, cancellationToken);
        return ValueTask.FromResult<object?>(
            $"Error: {Name} was refused: it carries {SecretMasker.ProviderPlaceholder}, which stands for a secret "
            + "you were not shown. Writing it would replace the real value. Nothing was written. Change the bytes "
            + "that hold the secret with run_command (for example sed on the file), without naming the value.");
    }

    private static bool Carries(object? value) => value switch
    {
        null => false,
        string text => text.Contains(SecretMasker.ProviderPlaceholder, StringComparison.Ordinal),
        JsonElement element => Carries(element),
        IDictionary dictionary => dictionary.Values.Cast<object?>().Any(Carries),
        IEnumerable items => items.Cast<object?>().Any(Carries),
        _ => false,
    };

    private static bool Carries(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => Carries(element.GetString()),
        JsonValueKind.Array => element.EnumerateArray().Any(Carries),
        JsonValueKind.Object => element.EnumerateObject().Any(p => Carries(p.Value)),
        _ => false,
    };
}
