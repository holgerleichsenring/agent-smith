using System.Text;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: the question put to a read-only instance about the FIRST-PARTY CLIENTS
/// of an interface — which operations they call, and which properties they send and read.
/// <para>
/// It asks for what was seen, never for what is missing: the difference is computed here,
/// and a reader asked to name unused capability would answer from the description in front
/// of it instead of from the code. A file it cannot decide is asked for by name, because a
/// missed call site turns an exercised operation into a finding.
/// </para>
/// </summary>
public static class ClientUsagePrompt
{
    public static string System() =>
        """
        You read source code and report what it calls. You have read-only tools; you do not
        modify, build or run anything.

        You are shown the operations an interface serves and the checkouts of the
        first-party clients that consume it. Find every place in those checkouts where a
        client calls that interface, and report per call site the operation it calls and
        the property names it SENDS in the request and READS from the response.

        Report only what the code shows. Never infer a call from a name, a comment, a test
        fixture or the served description. When you have read a file and cannot establish
        what it calls — the call is built from a variable, the client is generated, the
        file is too large to hold — list it under "undecided" with one short reason. That
        is not a failure: an undecided file is recorded and bounds the result, while a
        guess would report an operation the client really uses as one nobody uses.

        Answer with JSON and nothing else, no prose and no code fence:

          {"call_sites": [
             {"file": "<path as you addressed it>",
              "operation": "<operationId, or METHOD /path, as the served list names it>",
              "sends": ["<property name>", ...],
              "reads": ["<property name>", ...]}],
           "undecided": [{"file": "<path>", "why": "<one short sentence>"}]}

        Both arrays may be empty. An empty "call_sites" is a claim that these checkouts
        call the interface nowhere — make it only when you read them and that is true.
        """;

    public static string User(
        IReadOnlyList<string> consumerRepos, IReadOnlyList<ServedOperation> served)
    {
        ArgumentNullException.ThrowIfNull(consumerRepos);
        ArgumentNullException.ThrowIfNull(served);
        var builder = new StringBuilder();
        builder.AppendLine("CLIENT CHECKOUTS (address a path by prefixing it with the repository name):");
        foreach (var repo in consumerRepos) builder.AppendLine($"- {repo}");
        builder.AppendLine();
        builder.AppendLine("OPERATIONS THE INTERFACE SERVES:");
        foreach (var operation in served) builder.AppendLine(Describe(operation));
        builder.AppendLine();
        builder.AppendLine(
            "Read the client checkouts and report the call sites you find, in the JSON shape above.");
        return builder.ToString();
    }

    private static string Describe(ServedOperation operation)
    {
        var id = string.IsNullOrWhiteSpace(operation.OperationId)
            ? string.Empty
            : $" (operationId: {operation.OperationId})";
        return $"- {operation.Signature}{id}\n"
            + $"    accepts: {Names(operation.AcceptedProperties)}\n"
            + $"    returns: {Names(operation.ReturnedProperties)}";
    }

    private static string Names(IReadOnlyList<string> names) =>
        names.Count == 0 ? "(none)" : string.Join(", ", names);
}
