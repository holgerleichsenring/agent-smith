namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// p0504: the shape YamlDotNet actually deserializes a context.yaml into.
/// Extracted from <see cref="ContextYamlSerializer"/>, because an undeclared key
/// is dropped here WITHOUT a warning — which makes this the one file a new
/// context.yaml field is silently lost in if it is forgotten.
/// </summary>
internal sealed class ContextYamlReadShape
{
    public MetaBlock? Meta { get; set; }

    public StackBlock? Stack { get; set; }

    public string? Prerequisites { get; set; }

    // 2026-08-31-26d4: the ordered stages the repository declares as proof that a change
    // in it holds. Declared HERE first of all, because a key this file does not name is
    // dropped without a warning and every layer above it would then be reading a null
    // nothing ever filled.
    public List<VerifyBlock>? Verify { get; set; }

    // 2026-09-01-e14d: the files those stages were derived from, and their hash when
    // they were. Read via the shared UnderscoredNamingConvention as `verify_derived_from`.
    public DerivedFromBlock? VerifyDerivedFrom { get; set; }

    internal sealed class MetaBlock
    {
        public string? Workdir { get; set; }

        // p0331: what this context is for — surfaced for the scope classifier.
        public string? Purpose { get; set; }
    }

    internal sealed class StackBlock
    {
        public string? Lang { get; set; }

        // p0265: the analyzer/context-generator LLM names the exact toolchain Docker
        // image here (e.g. mcr.microsoft.com/dotnet/sdk:8.0, node:20-bookworm). It wins
        // over the language→image convention table — so any framework/version works
        // without a table row, and a net8 repo gets the 8.0 runtime that runs its tests.
        // 2026-08-25-014d: what the image may be is a registry question (operator policy,
        // ImageRegistryTrust); what it contains is discovered where it is used.
        public string? Image { get; set; }

        // p0268: LLM-authored k8s CPU/memory for this stack's sandbox. Read via the
        // shared UnderscoredNamingConvention (cpu_request, memory_limit, …).
        public ResourcesBlock? Resources { get; set; }
    }

    internal sealed class VerifyBlock
    {
        public string? Label { get; set; }

        public string? Command { get; set; }

        // Read via the shared UnderscoredNamingConvention as `when_present`.
        public string? WhenPresent { get; set; }
    }

    internal sealed class DerivedFromBlock
    {
        public List<string>? Files { get; set; }

        public string? Hash { get; set; }
    }

    internal sealed class ResourcesBlock
    {
        public string? CpuRequest { get; set; }

        public string? CpuLimit { get; set; }

        public string? MemoryRequest { get; set; }

        public string? MemoryLimit { get; set; }
    }
}
