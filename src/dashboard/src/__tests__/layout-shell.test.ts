import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// p0220: every content area must share the one width/padding policy
// (content-shell = full-bleed + 24px gutter). No route may reintroduce a
// centered max-w outlier.

const srcDir = join(dirname(fileURLToPath(import.meta.url)), "..");

const CONTENT_AREAS = [
  "app/page.tsx",
  "app/jobs/[id]/page.tsx",
  "components/system/SubsystemDetail.tsx",
  "components/execution/DetailPane.tsx",
];

const read = (rel: string) => readFileSync(join(srcDir, rel), "utf8");

describe("Layout content shell", () => {
  it("Layout_EveryContentArea_SameWidthAndPadding", () => {
    const missing = CONTENT_AREAS.filter((f) => !read(f).includes("content-shell"));
    expect(missing, `content areas not on the shared content-shell: ${missing.join(", ")}`).toEqual([]);
  });

  it("Layout_NoMaxW5xlOutlierRemains", () => {
    const offenders = CONTENT_AREAS.filter((f) => read(f).includes("max-w-5xl"));
    expect(offenders, `max-w-5xl outlier still present in: ${offenders.join(", ")}`).toEqual([]);
  });
});
