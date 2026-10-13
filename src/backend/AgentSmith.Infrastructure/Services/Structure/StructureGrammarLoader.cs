using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TreeSitter;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: loads a language's grammar library and compiles its query, once per
/// language. The library is resolved through NativeLibrary against the binding's assembly,
/// which follows deps.json and a single-file bundle's extraction — the binding's own loader
/// looks only beside the executable, where a single-file CLI has nothing.
/// </summary>
public sealed class StructureGrammarLoader(ILogger<StructureGrammarLoader> logger)
{
    private readonly ConcurrentDictionary<string, Lazy<StructureGrammar>> _grammars = new();

    public StructureGrammar For(StructureLanguage language) =>
        _grammars.GetOrAdd(language.Name, _ => new Lazy<StructureGrammar>(() => Load(language))).Value;

    private StructureGrammar Load(StructureLanguage language)
    {
        try
        {
            var handle = NativeLibrary.Load(language.Library, typeof(Language).Assembly, null);
            var entry = NativeLibrary.GetExport(handle, language.Symbol);
            var grammar = new Language(Marshal.GetDelegateForFunctionPointer<StructureLanguageEntry>(entry)());
            return new StructureGrammar(grammar, new Query(grammar, language.Query), null);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
                                       or BadImageFormatException or InvalidOperationException
                                       or ArgumentException)
        {
            logger.LogError(ex, "Structure grammar {Library} for {Language} could not be loaded",
                language.Library, language.Name);
            return StructureGrammar.Failed($"grammar {language.Library} could not be loaded: {ex.Message}");
        }
    }
}
