import { describe, expect, it } from "vitest";
import { readFileSync, readdirSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

// 2026-10-08-e8b9i: the dashboard asks through its own ConfirmDialog — never the browser's
// confirm, alert or prompt, which draw an unstyled system box with the origin above it. CALLS
// are matched, with comments stripped, so a comment that names the browser's dialog is not one.

const srcDir = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const NATIVE_CALL = /\b(window\.)?(confirm|alert|prompt)\s*\(/;

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return entry.name === "__tests__" ? [] : sourceFiles(path);
    return /\.(ts|tsx)$/.test(entry.name) && !/\.test\.(ts|tsx)$/.test(entry.name) ? [path] : [];
  });
}

/** The source with block and line comments removed; strings are left, which over-reports at worst. */
export function withoutComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:"'`])\/\/.*$/gm, "$1");
}

describe("native dialogs", () => {
  it("Dashboard_Source_CallsNoConfirmAlertOrPrompt", () => {
    const calls = sourceFiles(srcDir).flatMap((file) =>
      withoutComments(readFileSync(file, "utf8")).split("\n")
        .filter((line) => NATIVE_CALL.test(line))
        .map((line) => `${relative(srcDir, file)}: ${line.trim()}`));

    expect(calls).toEqual([]);
  });

  it("withoutComments_CommentNamingConfirm_IsNotACall", () => {
    expect(NATIVE_CALL.test(withoutComments("// window.confirm(x) was here\nconst a = 1;"))).toBe(false);
    expect(NATIVE_CALL.test(withoutComments("if (window.confirm(x)) go();"))).toBe(true);
  });
});
