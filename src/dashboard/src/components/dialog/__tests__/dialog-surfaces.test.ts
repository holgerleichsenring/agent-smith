import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// 2026-09-17-042ef: every SURFACE on the Work it out page is the Projects page's own — the
// panel card, the filled mark, the field label, the entity name, the tab and the button, each
// reached by joining the dialog's selector to the rule that already draws it in Config Studio.
// A component that draws one for itself instead is a second definition of a look the operator
// asked to be ONE look, and it will drift from the page it is meant to sit beside.
//
// Two ways that happens, and this scans for both: a surface written in theme utilities, and a
// local component (a chip, an eyebrow, a facts block) whose class string is that surface typed
// out again. Neither is a raw palette class, so neither is caught by the palette scan beside
// this one, and no rendering test notices a card that merely looks assembled.
//
// A colour word is NOT banned here — `text-ink`, `text-body` and `text-primary-deep` name the
// same values the mock tokens carry, and they are a type/tone decision, not a surface one.

const dialogDir = join(dirname(fileURLToPath(import.meta.url)), "..");

/** Utilities that draw a surface: a fill, a card line, a corner, a label's own type, a rule
 *  under a link. 2026-09-17-042ef, found by looking at the rendered page: the reference
 *  underlines NOTHING — a config entity's link is colour alone — and three underlined links in
 *  one pane read busier than the page it is meant to sit beside. Every link here goes through
 *  .d-link, which puts the rule on hover. */
const SURFACE = [
  "bg-canvas",
  "bg-primary",
  "border-mute",
  "border-primary",
  "rounded-md",
  "rounded-sm",
  "eyebrow-uppercase",
  "text-on-primary",
  "underline",
];

/** A local component that re-draws a surface the shared vocabulary already has. This list is a
 *  BACKSTOP on three names that were actually here, not the rule: renaming one evades it. What
 *  it cannot evade is the surface scan above — a local component that draws its own chip has to
 *  draw it out of something, and every utility it could draw one with is on the SURFACE list,
 *  whatever the function ends up being called. The name check catches the lazy regression; the
 *  utility check catches the determined one. */
const LOCAL = ["Chip", "Eyebrow", "Facts"];

/** Every component file under components/dialog, however deep; its tests are not components. */
function componentFiles(dir: string = dialogDir): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return entry.name === "__tests__" ? [] : componentFiles(path);
    return entry.isFile() && /\.(ts|tsx)$/.test(entry.name) ? [path] : [];
  });
}

/** The surface utilities in `content`, whatever variant prefix they were written behind. */
export function surfaceUtilities(content: string): string[] {
  return SURFACE.filter((utility) => content.includes(utility));
}

/** The locally declared chip/eyebrow/facts components in `content`. */
export function localSurfaces(content: string): string[] {
  return LOCAL.filter((name) => new RegExp(`function\\s+${name}\\s*\\(`).test(content));
}

// 2026-09-21-f237c: the panel's own track. Asserted on the class string, because jsdom resolves
// no container query — what is provable here is the policy the surface declares, and the policy
// is the thing that was wrong: 220px of one truncated line showed three words of a subject.
const surfacePath = join(dialogDir, "SpecDialogSurface.tsx");

describe("The dialog grid", () => {
  it("SpecDialog_TheWideBreakpoint_GivesThePanelItsLargerShare", () => {
    const grid = readFileSync(surfacePath, "utf8");

    expect(grid).toContain("@6xl:grid-cols-[300px_minmax(0,1fr)_360px]");
    // The scope pane keeps its width: it renders given names this page cannot shorten. The
    // exchange is prose, and it is what reflows.
    expect(grid).not.toContain("@6xl:grid-cols-[300px_minmax(0,1fr)_300px]");
    // The mid and phone breakpoints are untouched — the panel is already full width there.
    expect(grid).toContain("@3xl:grid-cols-[minmax(0,1fr)_300px]");
  });
});

describe("Dialog surfaces", () => {
  it("DialogComponents_SurfaceScan_FindsNoThemeDrawnCardMarkOrButton", () => {
    const files = componentFiles();
    expect(files.length).toBeGreaterThan(5);

    const offenders = files.flatMap((file) =>
      surfaceUtilities(readFileSync(file, "utf8")).map((hit) => `${file}: ${hit}`));

    expect(offenders, `surfaces drawn in theme utilities:\n${offenders.join("\n")}`).toEqual([]);
  });

  it("DialogComponents_SurfaceScan_FindsNoLocalChipEyebrowOrFacts", () => {
    const files = componentFiles();

    const offenders = files.flatMap((file) =>
      localSurfaces(readFileSync(file, "utf8")).map((hit) => `${file}: ${hit}`));

    expect(offenders, `locally declared surfaces:\n${offenders.join("\n")}`).toEqual([]);
  });

  // Proves both rules bite without waiting for someone to write the violation again.
  it("Rule_HasTeeth_ASurfaceUtilityAndALocalChipAreFlaggedAndTheTonesAreNot", () => {
    expect(surfaceUtilities('className="rounded-md border border-mute bg-canvas underline"')).toEqual([
      "bg-canvas",
      "border-mute",
      "rounded-md",
      "underline",
    ]);
    expect(surfaceUtilities('className="ecard inert dsh-body text-ink text-primary-deep"')).toEqual([]);
    expect(localSurfaces("function Chip({ children }: { children: string }) {")).toEqual(["Chip"]);
  });

  // And that a RENAMED local chip does not get past both of them: the name list would miss it,
  // the utility list cannot, because the thing it draws itself out of is on that list.
  it("Rule_HasTeeth_ARenamedLocalChipIsStillCaughtByWhatItDrawsItselfWith", () => {
    const renamed = 'function Tag({ t }: { t: string }) {\n'
      + '  return <span className="rounded-sm border border-mute px-1">{t}</span>;\n}';
    expect(localSurfaces(renamed)).toEqual([]);
    expect(surfaceUtilities(renamed)).toEqual(["border-mute", "rounded-sm"]);
  });
});
