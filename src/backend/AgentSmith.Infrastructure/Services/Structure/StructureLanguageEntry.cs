using System.Runtime.InteropServices;

namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: the shape of a grammar library's entry point, tree_sitter_&lt;language&gt;,
/// which returns the language pointer the binding wraps.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr StructureLanguageEntry();
