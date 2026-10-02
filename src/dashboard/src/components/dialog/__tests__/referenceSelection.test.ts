import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import {
  REBUILDABLE_FOLDERS,
  entriesOf,
  gitignoresOf,
  holdsCredentials,
  leftOutOf,
  rebuildableFolderOf,
  sentBy,
  totalsOf,
} from "../referenceSelection";

// 2026-10-02-075db: what a pick sends, worked out before the body that the server bounds.
const MB = 1024 * 1024;

function picked(path: string, size = 1, content = "x"): File {
  const file = new File([content], path.split("/").at(-1)!);
  Object.defineProperty(file, "webkitRelativePath", { value: path });
  Object.defineProperty(file, "size", { value: size });
  return file;
}

function entry(entries: ReturnType<typeof entriesOf>, name: string) {
  const found = entries.find((e) => e.name === name);
  if (!found) throw new Error(`no entry ${name} in ${entries.map((e) => e.name).join(", ")}`);
  return found;
}

const paths = (files: File[]) => files.map((f) => f.webkitRelativePath);

describe("referenceSelection", () => {
  it("referenceSelection_AFolderWithAVenvAndNodeModules_PreUnticksThemAsRebuildable", () => {
    const entries = entriesOf([
      picked("app/.venv/lib/a.py"), picked("app/node_modules/x/i.js"), picked("app/src/main.py"),
    ], {});

    expect(entries.map((e) => e.name)).toEqual([".venv/", "node_modules/", "src/"]);
    expect(entry(entries, ".venv/")).toMatchObject({ ticked: false, reason: "rebuildable" });
    expect(entry(entries, "node_modules/")).toMatchObject({ ticked: false, reason: "rebuildable" });
    expect(entry(entries, "src/").ticked).toBe(true);
  });

  it("referenceSelection_ARootGitignore_UnticksDistLeavesOutLogsAndKeepsTheEnv", async () => {
    const files = [
      picked("app/.gitignore", 20, "dist\n*.log\n.env\n"), picked("app/.env"),
      picked("app/dist/bundle.js"), picked("app/src/main.ts"), picked("app/src/debug.log"),
    ];
    const entries = entriesOf(files, await gitignoresOf(files));

    expect(entry(entries, "dist/")).toMatchObject({ ticked: false, reason: ".gitignore" });
    expect(paths(sentBy(entry(entries, "src/"), true))).toEqual(["app/src/main.ts"]);
    expect(entry(entries, ".env")).toMatchObject({ ticked: true });
    // Ticking an entry that started unticked re-adds what the .gitignore left out under it.
    expect(paths(sentBy(entry(entries, "dist/"), true))).toEqual(["app/dist/bundle.js"]);
  });

  it("referenceSelection_ANestedGitignore_AppliesBelowItsOwnFolder", async () => {
    const files = [
      picked("app/web/.gitignore", 10, "build/\n"), picked("app/web/build/a.js"),
      picked("app/web/index.ts"), picked("app/build/kept.js"),
    ];
    const entries = entriesOf(files, await gitignoresOf(files));

    expect(paths(sentBy(entry(entries, "web/"), true))).toEqual(["app/web/.gitignore", "app/web/index.ts"]);
    expect(entry(entries, "build/").ticked).toBe(true);
  });

  it("referenceSelection_NestedNodeModulesAndAnOversizedFile_AreLeftOutAndCounted", () => {
    const entries = entriesOf([
      picked("app/frontend/src/a.ts"), picked("app/frontend/node_modules/x/i.js"),
      picked("app/frontend/video.mp4", 30 * MB),
    ], {});
    const frontend = entry(entries, "frontend/");

    expect(frontend.ticked).toBe(true);
    expect(paths(sentBy(frontend, true))).toEqual(["app/frontend/src/a.ts"]);
    expect(leftOutOf(entries, [true])).toEqual({
      count: 2,
      summary: ["1 file under frontend/ (rebuildable)", "1 file under frontend/ (over 25 MB)"],
    });
  });

  it("referenceSelection_A52MbFile_IsUntickedOver25MbAndTotalsOverTheBoundSayByHowMuch", () => {
    const entries = entriesOf([picked("big.bin", 52 * MB), picked("a.html", 10)], {});

    expect(entry(entries, "big.bin")).toMatchObject({ ticked: false, reason: "over 25 MB" });
    expect(totalsOf([picked("a", 20 * MB), picked("b", 10 * MB)]).over).toBe("5 MB over the 25 MB a set may hold");
    expect(totalsOf(Array.from({ length: 502 }, (_, i) => picked(`f${i}`))).over)
      .toBe("2 files over the 500 a set may hold");
    expect(totalsOf([picked("a", 10)])).toEqual({ files: 1, bytes: 10, over: null });
  });

  it("referenceSelection_LeftOut_NamesUntickedEntriesWhole", () => {
    const entries = entriesOf([picked("app/.venv/a.py"), picked("app/notes/a.md"), picked("app/src/b.ts")], {});

    expect(leftOutOf(entries, [false, false, true])).toEqual({
      count: 2, summary: [".venv/ (rebuildable)", "notes/ (unticked)"],
    });
  });

  it("referenceSelection_APlainPick_KeepsEveryFileAsItsOwnEntry", () => {
    const entries = entriesOf([new File(["a"], "a.html"), new File(["b"], "b.css")], {});

    expect(entries.map((e) => e.name)).toEqual(["a.html", "b.css"]);
  });

  it("referenceSelection_RebuildableFolderOf_MirrorsFolderOf", () => {
    expect(rebuildableFolderOf("site/node_modules/a/b.js")).toBe("site/node_modules/");
    expect(rebuildableFolderOf("site/node_modules")).toBeNull();
    expect(rebuildableFolderOf("site/src/a.js")).toBeNull();
  });

  // The same cases as ReferenceSetValidatorTests.ReferenceCredentialFiles_Holds_NamesTheCommonOnes.
  it("referenceSelection_HoldsCredentials_NamesTheCommonOnes", () => {
    for (const path of ["a/.env", "a/.env.local", "a/runtime.env", "a/.npmrc", "a/tls.pem", "a/id_rsa", "a/credentials.json"])
      expect(holdsCredentials(path), path).toBe(true);
    expect(holdsCredentials("a/.env.example.md")).toBe(true);
    expect(holdsCredentials("a/app.py")).toBe(false);
  });

  it("referenceSelection_RebuildableList_EqualsReferenceRebuildableFoldersCs", () => {
    const cs = readFileSync(
      "../backend/AgentSmith.Server/Services/References/ReferenceRebuildableFolders.cs", "utf8");
    const list = /Names\s*=\s*\[([\s\S]*?)\];/.exec(cs)?.[1] ?? "";
    const names = [...list.matchAll(/"([^"]+)"/g)].map((m) => m[1]);

    expect(names.length).toBeGreaterThan(0);
    expect(REBUILDABLE_FOLDERS).toEqual(names);
  });
});
