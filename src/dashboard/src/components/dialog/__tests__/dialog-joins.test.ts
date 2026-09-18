import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// 2026-09-17-042ef: the phase's whole method is that this page BORROWS the Projects page's
// rules rather than owning a copy of them — the dialog's selector is added to the rule that
// already draws the card, the mark, the field label, the entity name, the tab and the button.
// Nothing tested that. Every component test would stay green if someone replaced the joins
// tomorrow with a .mock-dialog block restating those declarations, and this file's own decisions
// argue at length that a copy drifts, citing .mock-system .btn — a copy that has already drifted
// by one declaration from the rule it was copied from.
//
// So the stylesheet is read here, and three things are asserted about it.

const cssPath = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "styles", "mock-parity.css");

/** Where this page's OWN structure begins. Above it, a .mock-dialog selector may only join. */
const OWN_BLOCK = "===== Work it out surface (2026-09-17-042ef)";

/** The shared classes the dialog joins rather than owns. A rule in the own block naming one of
 *  these with NO further class on it is a second definition of it. */
const JOINED = ["ecard", "ec-mark", "ec-marks", "ec-name", "ec-sub", "fl", "fv", "btn", "dtab"];

interface Rule {
  selector: string;
  body: string;
  at: number;
}

/** Every innermost rule in the sheet. The inner pattern crosses no brace, so a rule inside an
 *  @media wrapper is found on its own and the wrapper is never mistaken for a selector. */
export function rulesIn(css: string): Rule[] {
  return [...css.matchAll(/([^{}]*)\{([^{}]*)\}/g)].map((m) => ({
    selector: m[1].replace(/\/\*[\s\S]*?\*\//g, "").trim(),
    body: m[2].trim(),
    // The brace, not the match start: the match begins after the PREVIOUS rule's brace, so a
    // rule introduced by a comment would otherwise be placed at the comment's start and read
    // as living one section higher than it does.
    at: (m.index ?? 0) + m[1].length,
  }));
}

const css = readFileSync(cssPath, "utf8");
const ownFrom = css.indexOf(OWN_BLOCK);
const dialogRules = rulesIn(css).filter((r) => r.selector.includes(".mock-dialog"));

/** A selector list that also names one of the pages this rule is borrowed from. */
export function isAJoin(selector: string): boolean {
  return selector.includes(".mock-config") || selector.includes(".mock-runs");
}

/** The shared class this selector redefines outright, if any: the class is the last thing on
 *  the selector, so no modifier narrows it. `.mock-dialog .fl.on` narrows; `.mock-dialog .fl`
 *  does not, and is a copy of the rule the dialog is supposed to be joining. */
export function redefines(selector: string): string[] {
  return selector
    .split(",")
    .flatMap((one) => JOINED.filter((cls) => new RegExp(`\\.${cls}\\s*$`).test(one.trim())));
}

describe("Dialog joins", () => {
  it("MockParity_EveryDialogSelectorOutsideItsOwnBlock_JoinsAConfigOrRunsRule", () => {
    expect(ownFrom).toBeGreaterThan(0);
    expect(dialogRules.length).toBeGreaterThan(20);

    const copies = dialogRules
      .filter((r) => r.at < ownFrom && !isAJoin(r.selector))
      .map((r) => r.selector);

    expect(copies, `.mock-dialog rules that stand alone instead of joining:\n${copies.join("\n")}`)
      .toEqual([]);
  });

  it("MockParity_TheOwnStructureBlock_RedefinesNoBorrowedClass", () => {
    const copies = dialogRules
      .filter((r) => r.at >= ownFrom && redefines(r.selector).length > 0)
      .map((r) => r.selector);

    expect(copies, `borrowed classes redefined instead of joined:\n${copies.join("\n")}`)
      .toEqual([]);
  });

  // The one that cost this page its typography. `font:` is a SHORTHAND — it resets family, size,
  // weight and line-height — and mock-parity.css is unlayered, so it beats every font utility and
  // every mock font class composed onto the same element. It is a UA-chrome reset, and the only
  // elements with UA chrome to reset are the form ones. Nothing in jsdom can see this.
  it("MockParity_TheFontShorthand_IsOnlyEverOnAFormElement", () => {
    const loose = dialogRules
      .filter((r) => r.at >= ownFrom && /(^|;)\s*font\s*:/.test(r.body))
      .filter((r) => !/\b(button|input|select|textarea)\./.test(r.selector))
      .map((r) => r.selector);

    expect(loose, `the font shorthand on a selector that is not a form element:\n${loose.join("\n")}`)
      .toEqual([]);
  });

  it("Rule_HasTeeth_AStandaloneRuleARedefinitionAndALooseFontAreAllSeen", () => {
    expect(isAJoin(".mock-config .ecard, .mock-dialog .ecard")).toBe(true);
    expect(isAJoin(".mock-dialog .ecard")).toBe(false);
    expect(redefines(".mock-dialog .fl")).toEqual(["fl"]);
    expect(redefines(".mock-dialog .fl.on")).toEqual([]);
    expect(redefines(".mock-dialog .ecard.waiting")).toEqual([]);
    expect(rulesIn("@media (max-width:1px){.a,.b{padding:0}}")[0].selector).toBe(".a,.b");
  });
});
