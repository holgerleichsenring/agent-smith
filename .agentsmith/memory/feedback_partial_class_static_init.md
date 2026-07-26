---
name: feedback_partial_class_static_init
description: "C# does not guarantee static-field initialization order across partial-class files; use a static constructor for cross-file references"
metadata:
  type: feedback
status: proposed
---
When splitting a static class into partial files, C# does not guarantee initialization order of static fields across the files. If file A's static field initializer references file B's static field, the reference may resolve to `null` at compile-emit time depending on compiler file ordering.

**Why:** Concrete incident in p0147h (PR #145): `PipelinePresets.cs` had `All = new Dictionary<string, IReadOnlyList<string>> { ["fix-bug"] = FixBug, ... }` as an inline static initializer; `FixBug` lived in `PipelinePresets.FixBug.cs`. The compiler initialized `All` before `FixBug` was populated → 52 test failures. The agent caught it during the test run and fixed it.

**How to apply:** When refactoring a static class into `static partial class` files (the p0147h pattern), default to a `static` constructor for any field whose value depends on another field declared in a different partial file:

```csharp
// Wrong — inline init may run before FixBug is initialized
public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> All =
    new Dictionary<string, IReadOnlyList<string>> { ["fix-bug"] = FixBug };

// Right — static constructor runs after all field initializers
public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> All;
static PipelinePresets() { All = new Dictionary<string, IReadOnlyList<string>> { ["fix-bug"] = FixBug }; }
```

Same applies to lists/arrays of references (`Names = All.Keys.ToList()` etc.). Self-contained static fields (constants, `new()` with no cross-file refs) are fine inline. The discipline is: if it references another partial's field, move it into the static ctor.
