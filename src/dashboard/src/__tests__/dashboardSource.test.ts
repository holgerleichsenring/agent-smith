import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

// 2026-11-22-7103a: the dashboard names no tool of the master's — what a run did is read from its specs' standing and
// its verify stages, so no source the dashboard writes says finish_spec. Generated files (.gitignore: api.ts,
// tokens.json) and tests, which may quote the server's text, are not the dashboard's own words.

const GENERATED = new Set([join("src", "types", "api.ts"), join("src", "styles", "tokens.json")]);

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return name === "__tests__" ? [] : sources(path);
    return /\.(ts|tsx|css)$/.test(name) && !/\.test\.tsx?$/.test(name) && !GENERATED.has(path) ? [path] : [];
  });
}

describe("dashboard source", () => {
  it("DashboardSource_NoFinishSpecOutsideGenerated", () => {
    const naming = sources("src").filter((path) => readFileSync(path, "utf8").includes("finish_spec"));
    expect(naming).toEqual([]);
  });
});
