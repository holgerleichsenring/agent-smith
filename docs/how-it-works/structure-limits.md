# Structure limits

A coding agent left alone grows methods. It adds the one extra branch, then the one extra lookup, and three specs later a handler has 140 lines and nobody decided that. A principle that says "keep methods short" doesn't stop it, because a principle is a sentence and the model reads past sentences when it is busy.

So the size rules a context cares about are written down as numbers, measured with a parser, and checked at every spec commit. The code that already breaks them on the day you install them is recorded once and tolerated, so a limit is green the day it arrives and only new damage fails.

## Where the limits live

In the context's `principles.md`, as a YAML fence under a `Limits` heading. The language delta in the skills catalog brings one, under `## Limits`, and the init copies it in with the rest of the principles:

````markdown
## Limits

Tolerance: 25 percent — a unit past its limit by at most a quarter is a
divergence the pull request names; past that it is an excess the commit gate
sends back.

```yaml
function_lines: 55
tolerance_percent: 25
```
````

Your own house standard goes under `### Limits` inside the `## Project Specifics` section, and it overrides the delta key by key, because it is read last. A key you don't set keeps the delta's value.

````markdown
## Project Specifics (ratified additions)

### Limits

```yaml
function_lines: 40
file_lines: 400
```
````

The keys are a closed list:

| Key | What it limits |
|---|---|
| `function_lines` | lines of one function, method, constructor or property |
| `type_lines` | lines of one class, struct, interface, record or enum |
| `file_lines` | lines of one file |
| `types_per_file` | how many types one file declares |
| `tolerance_percent` | the band above every limit, in percent; 0 makes the context strict |

Every value is a positive whole number (`tolerance_percent` may be 0). A metric you leave out sets no limit. A fence that carries only `tolerance_percent` limits nothing. Anything else in the fence is ignored here; the catalog's own validator refuses it before it ever ships.

## What gets measured

A tree-sitter parser reads the file and finds its types and functions. Today it knows C# (`.cs`) and Rust (`.rs`). A file in another language isn't measured, and a context whose files are all in other languages has nothing to judge. Test code and generated code are left out by path: for C# that is test projects and `tests/` folders, `*.g.cs`, `*.Designer.cs` and `Migrations/`; for Rust the `tests/` and `benches/` trees.

Which context a file belongs to comes from the context's `meta.workdir` and its `sources:` list, the directories its libraries and test projects live in. The longest directory that holds the file wins. That is on [the context file](../reference/concepts/context-file.md#sources-are-the-rest-of-the-component).

## The tolerance band

A limit isn't a cliff. With `function_lines: 55` and `tolerance_percent: 25` the band ends at 68 lines (55 × 125 / 100, rounded down):

- up to 55 lines: fine, nobody says anything
- 56 to 68: a **divergence**. The gate lets it through, and the repository's pull request lists it under "Structure within tolerance", one line per unit, so the reviewer decides whether it is solid.
- 69 and more: an **excess**. That's what the commit gate sends back, and what the baseline records.

## The baseline

When the init writes a context's principles, it measures every file the context owns and writes every unit already past the band to `structure-baseline.tsv`, right next to `principles.md`:

```
# path	key	metric	value — units over their Limits when measured, each tolerated up to its value
# measured with: function_lines=55 tolerance_percent=25
# owned:	src/Inventory.Api	src/Inventory.Core
src/Inventory.Core/Pricing.cs	Calculate	function_lines	91
src/Inventory.Core/Pricing.cs	PriceRules	type_lines	420
```

One tab-separated line per unit: path, the unit's name, the metric and the value it had. The header records the limits the entries were measured with and the directories the context owned at the time. The init PR says how many units went in.

An entry tolerates its unit up to the recorded value. `Calculate` may stay at 91 lines, or shrink, and nobody complains. If a spec makes it 92, it is a new excess. A unit not in the file has no allowance.

A later init with **Refresh principles** refreshes the baseline, and the file only gets better from it. An entry whose unit is still past the band stays, at the lower of its old and new value; one that came back inside the band disappears. A unit is added only when a changed limit exposes it, compared with the limits in the header (the principles text may already have been edited by hand), or when it sits in a directory the context newly owns. A unit that simply grew past an unchanged limit is not added; it shows up at the commit gate, where it belongs. A baseline written before the header existed has no recorded limits, so its refresh adds every excess it doesn't list.

Without a baseline, a context isn't judged at all. An init that keeps an existing `principles.md` keeps an existing baseline untouched (apart from new owned directories), and no baseline is written when the principles declare no limits or were written by an old catalog's bootstrap skill. The init PR says which of these happened.

## The commit gate

`CommitSpecWork`, the step that puts a spec's work on the branch, runs the formatter the context declares (see [`format`](../reference/concepts/context-file.md#format-is-the-repositorys-own-formatter)) and then judges the structure. For each repository of the run it:

1. finds the commit the branch was cut from: the merge-base of `HEAD` with the base the branch is delivered against, falling back to the commit the run started on
2. reads every context's limits and baseline as they are at that commit
3. lists the files the branch changed since then, including new files, and measures each one as the working tree holds it
4. drops what the baseline tolerates and keeps the rest as findings

Reading the terms at the merge-base matters. A master that edits `principles.md` or the baseline can't loosen its own gate, and when the base moved on after the cut, other people's files don't land on this branch's bill. Because the files are measured against the whole branch, a file an earlier spec changed is judged again at every later spec commit. A repository with several toolchain sandboxes has every one of them judged; the same unit seen in two sandboxes counts once, at the worse value.

What happens next:

- No excess: the step passes and says how much it judged, "structure judged 12 file(s) in 2 context(s), within their Limits". Divergences go to the pull request.
- The first excess in a spec: the master gets one correction pass, with every finding as `path unit: metric value > limit`. Master, open questions and commit run again, ahead of `VerifySpec`.
- An excess after that pass: the spec fails, recorded with every finding. The sequence stops there like any other red spec.
- Inside the fix pass that follows the diff review, any finding undoes the fix, as a red build does.
- A grammar that won't load fails the spec too, with the reason.

## When it judged nothing

A gate that passed over nothing is easy to mistake for a green one, so the step result says it. "structure not judged" is followed by why, per repository:

- no comparable base (no merge-base with any base the branch could be delivered against)
- no context with Limits and a baseline at the merge-base
- git couldn't list the changed files
- a sandbox of the repository has no comparable base of its own

When some contexts were judged and others weren't, the line names both. A file the sandbox won't hand over (too large, or not UTF-8) is logged and skipped.

The usual reason on a freshly onboarded repository is the second one: the baseline is written by the init, and the gate reads it from the base. Until the init PR is merged into the branch your tickets start from, there is nothing to judge against.

## Next

- [The context file](../reference/concepts/context-file.md) — `format`, `sources` and the rest of `context.yaml`.
- [Bootstrap](../reference/skills/bootstrap.md) — how the init composes the principles.
- [The code pipeline](../reference/pipelines/fix-and-feature.md#the-structure-gate) — where the gate sits in a run.
