namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-12-fbbdb: the formatter a repository ALREADY runs, read from the <c>format:</c>
/// block of its own context.yaml, in the form that rewrites files rather than checks them.
/// <para>
/// The spec commit runs it over the files the spec changed, before the push, so layout is
/// the repository's formatter's job — never a done criterion and never a gate. Only the
/// spec's files: a repository-wide reformat would drag untouched code into the diff, the
/// secret scan and the structure baseline.
/// </para>
/// </summary>
/// <param name="Command">The command line, run through /bin/sh -c at the REPOSITORY ROOT. It
/// carries <see cref="FilesPlaceholder"/>, which is replaced by the shell-quoted,
/// repository-root-relative paths the spec changed under the declaring context's workdir.</param>
/// <param name="WhenPresent">Optional path, relative to the repository root, the command needs
/// in order to mean anything; absent =&gt; the formatter is not run.</param>
public sealed record ContextYamlFormat(string Command, string? WhenPresent = null)
{
    /// <summary>The token a format command must carry, replaced by the changed paths.</summary>
    public const string FilesPlaceholder = "{files}";
}
