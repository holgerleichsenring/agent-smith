import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// p0220: every content area shares one width/padding policy. p0343c splits the
// policy in two: the REDESIGNED surfaces (runs home, run viewer, config studio)
// live on the ratified mocks' shell (.mock-shell + the mock .main/.wrap), while
// the System/Rollups panes stay on the shared content-shell. No route may
// reintroduce a centered max-w outlier of its own.

const srcDir = join(dirname(fileURLToPath(import.meta.url)), "..");

const MOCK_SURFACES = [
  "app/page.tsx",
  "app/jobs/[id]/page.tsx",
  "components/config/ConfigStudio.tsx",
];

const CONTENT_SHELL_AREAS = [
  "components/system/SubsystemDetail.tsx",
  "components/execution/DetailPane.tsx",
];

const read = (rel: string) => readFileSync(join(srcDir, rel), "utf8");

describe("Layout content shell", () => {
  it("Layout_RedesignedSurfaces_CarryTheMockShell", () => {
    const missing = MOCK_SURFACES.filter((f) => !read(f).includes("mock-shell"));
    expect(missing, `redesigned surfaces missing the mock-shell wrapper: ${missing.join(", ")}`).toEqual([]);
  });

  it("Layout_SystemAreas_StayOnContentShell", () => {
    const missing = CONTENT_SHELL_AREAS.filter((f) => !read(f).includes("content-shell"));
    expect(missing, `content areas not on the shared content-shell: ${missing.join(", ")}`).toEqual([]);
  });

  it("Layout_NoMaxW5xlOutlierRemains", () => {
    const offenders = [...MOCK_SURFACES, ...CONTENT_SHELL_AREAS].filter((f) =>
      read(f).includes("max-w-5xl"),
    );
    expect(offenders, `max-w-5xl outlier still present in: ${offenders.join(", ")}`).toEqual([]);
  });
});
