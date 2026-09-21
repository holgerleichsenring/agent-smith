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

// 2026-09-18-e63d: text this page did not choose — a repository, a template, a project, a
// revision — was clipped at the card edge, and the fix is one rule this file's own guards
// allow in exactly one shape: a trailing modifier inside the own structure block, because a
// bare .mock-dialog .ec-mark or .ec-name would trip the redefinition guard above. Both halves
// of it are asserted here, and so is the reach of every OTHER rule that could make the same
// four classes wrap on the Projects page. This project builds no CSS for a test and resolves
// no cascade: what a stylesheet guard can see is the declarations that were typed.

/** The modifier, and the four borrowed classes it must land on. A rule carrying the modifier
 *  on the mark alone would leave the heading one line above it sliced. */
const GIVEN = ".given";
const BORROWED = ["ec-mark", "ec-name", "ec-sub", "fv"];

/** A page root. .mock-shell is NOT one — it is on every mock page, so a rule scoped only to
 *  the shell reaches the Projects page like an unscoped one does. */
const PAGE_ROOTS = ["mock-config", "mock-dialog", "mock-runs", "mock-viewer", "mock-system",
  "mock-access", "mock-overview", "mock-diagnostic"];

/** A property that lets a long unbroken token break, or that lifts a nowrap. */
const BREAKS = /(^|;)\s*(overflow-wrap|word-wrap|word-break|line-break|hyphens)\s*:/;
const WRAPS = /(^|;)\s*white-space\s*:\s*(normal|pre-wrap|pre-line)\s*(;|$)/;

/** The borrowed classes this selector part names, as a class and not as a prefix of a longer
 *  one — `.ec-marks` is a row, not a mark, and `.fv` is not `.fvx`. */
export function borrowedIn(part: string): string[] {
  return BORROWED.filter((cls) => new RegExp(`\\.${cls}(?![\\w-])`).test(part));
}

/** Whether this selector part can match an element on the Projects page: it either names that
 *  page, or it names no page at all and so applies on every one of them. The second half is
 *  the hole a `.mock-config`-only check leaves — `.mock-shell .ec-mark` is on every page root
 *  and sits later in the file than the rule it would override. */
export function reachesProjects(part: string): boolean {
  return part.includes(".mock-config") || !PAGE_ROOTS.some((root) => part.includes(`.${root}`));
}

describe("Given text wraps, and only on this page", () => {
  it("MockParity_TheGivenTextModifier_CarriesNormalWhiteSpaceAndOverflowWrapAnywhere", () => {
    const modifier = rulesIn(css).filter((r) =>
      r.selector.split(",").every((one) => one.trim().endsWith(GIVEN)) && r.selector.includes(GIVEN));
    expect(modifier, `no rule whose every selector ends in ${GIVEN}`).toHaveLength(1);

    // All four surfaces, or the heading is sliced while the marks below it wrap cleanly.
    const covered = modifier[0].selector.split(",").flatMap((one) => borrowedIn(one.trim()));
    expect([...covered].sort()).toEqual([...BORROWED].sort());

    // The reset. Without it the break property below has no soft-wrap opportunity to use.
    expect(modifier[0].body, "the modifier must lift the mark rule's nowrap").toMatch(WRAPS);
    // `anywhere` and not `break-word`: these are flex items, and break-word does not
    // contribute to min-content sizing, so they would refuse to shrink and stay clipped.
    expect(modifier[0].body, "overflow-wrap must be anywhere, not break-word")
      .toMatch(/(^|;)\s*overflow-wrap\s*:\s*anywhere\s*(;|$)/);
  });

  // Named as the absence it is. Nothing here resolves a cascade, so "the Projects page renders
  // exactly as it did" is not observable. What IS observable: every rule in this sheet that can
  // reach one of the four borrowed classes on that page is free of a break or wrap declaration,
  // and the mark rule it shares with this page still says nowrap.
  it("MockParity_NoRuleReachingTheProjectsPage_LetsABorrowedClassWrap", () => {
    const shared = rulesIn(css).find((r) => r.selector === ".mock-config .ec-mark, .mock-dialog .ec-mark");
    expect(shared, "the shared mark rule is gone").toBeDefined();
    expect(shared!.body, "the shared mark rule must still pin a mark to one line")
      .toMatch(/(^|;)\s*white-space\s*:\s*nowrap\s*(;|$)/);

    const wrapping = rulesIn(css)
      .filter((r) => BREAKS.test(r.body) || WRAPS.test(r.body))
      .filter((r) => r.selector.split(",").map((one) => one.trim())
        .some((one) => reachesProjects(one) && borrowedIn(one).length > 0))
      .map((r) => r.selector);

    expect(wrapping, `a borrowed class was made to wrap on the Projects page:\n${wrapping.join("\n")}`)
      .toEqual([]);
  });

  it("Rule_HasTeeth_AShellScopedRuleIsSeenAndAnotherPagesIsNot", () => {
    expect(reachesProjects(".mock-config .ec-mark")).toBe(true);
    expect(reachesProjects(".mock-shell .ec-mark")).toBe(true);
    expect(reachesProjects(".ec-mark")).toBe(true);
    expect(reachesProjects(".mock-dialog .ec-mark.given")).toBe(false);
    expect(reachesProjects(".mock-viewer .ident .fv")).toBe(false);
    expect(borrowedIn(".mock-config .ec-marks")).toEqual([]);
    expect(borrowedIn(".mock-config .ec-mark.warn")).toEqual(["ec-mark"]);
    expect(borrowedIn(".mock-config .fields .fv.link")).toEqual(["fv"]);
    expect(borrowedIn(".mock-config .tpl-ctx")).toEqual([]);
    expect(BREAKS.test("overflow-wrap: anywhere")).toBe(true);
    expect(BREAKS.test("font-size: 11px; word-break: break-all")).toBe(true);
    expect(BREAKS.test("border-radius: 4px")).toBe(false);
    expect(WRAPS.test("white-space: normal")).toBe(true);
    expect(WRAPS.test("white-space: nowrap")).toBe(false);
  });
});

// 2026-09-20-4b0ae: the acknowledgement an inspect leaves is DRAWN, or the phase shipped two
// attributes nothing paints. Both rules live in this page's own block — a new name below the
// marker is the one shape the two guards above allow — and the focus one must be a plain
// :focus, because the focus it draws follows a mouse click on a control in another column and
// matches no :focus-visible rule. Nothing here resolves a cascade; what is observable is the
// declarations that were typed.
describe("Inspecting is drawn", () => {
  it("MockParity_TheDialogBlock_DrawsTheMarkAndAPlainFocusOutlineOnThePanel", () => {
    const own = dialogRules.filter((r) => r.at >= ownFrom);

    const mark = own.filter((r) => r.selector.includes('[data-inspected="true"]'));
    expect(mark, "no rule draws the mark an inspect puts on the pane").toHaveLength(1);
    expect(mark[0].selector).toContain(".d-pane");
    expect(mark[0].body, "the mark must change something a person can see")
      .toMatch(/(^|;)\s*(border-color|box-shadow|outline|background)\s*:/);

    const focused = own.filter((r) => /\.d-panel:focus(?![\w-])/.test(r.selector));
    expect(focused, "no rule draws the focus the inspect moves to the panel").toHaveLength(1);
    expect(focused[0].body, "the focused panel must be outlined").toMatch(/(^|;)\s*outline\s*:/);

    // The one that would have shipped an invisible fix: a programmatic focus after a mouse
    // click matches :focus-visible in none of the browsers this runs in.
    const visibleOnly = own.filter((r) => /\.d-panel:focus-visible/.test(r.selector));
    expect(visibleOnly, "the panel's focus rule may not be a focus-visible one").toEqual([]);
  });
});
