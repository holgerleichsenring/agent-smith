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

    internal sealed class MetaBlock
    {
        public string? Workdir { get; set; }

        // p0331: what this context is for — surfaced for the scope classifier.
        public string? Purpose { get; set; }

        // p0504: one word naming a profile in the resolved skills catalog. The
        // profile brings a toolchain image and the ordered verify commands, so a
        // context declaring a domain need not name a stack.image at all.
        public string? Domain { get; set; }
    }

    internal sealed class StackBlock
    {
        public string? Lang { get; set; }

        // p0265: the analyzer/context-generator LLM names the exact toolchain Docker
        // image here (e.g. mcr.microsoft.com/dotnet/sdk:8.0, node:20-bookworm). It wins
        // over the language→image convention table — so any framework/version works
        // without a table row, and a net8 repo gets the 8.0 runtime that runs its tests.
        public string? Image { get; set; }

        // p0268: LLM-authored k8s CPU/memory for this stack's sandbox. Read via the
        // shared UnderscoredNamingConvention (cpu_request, memory_limit, …).
        public ResourcesBlock? Resources { get; set; }
    }

    internal sealed class ResourcesBlock
    {
        public string? CpuRequest { get; set; }

        public string? CpuLimit { get; set; }

        public string? MemoryRequest { get; set; }

        public string? MemoryLimit { get; set; }
    }
}
