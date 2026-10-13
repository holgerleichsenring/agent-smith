import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// 2026-10-13-09f5: 'this verdict is wrong' carried a class no stylesheet defined, so it rendered as
// large unstyled text under every criterion.
describe("criterion overrule link", () => {
  it("mock-parity.css styles the criterion's overrule link", () => {
    const css = readFileSync("src/styles/mock-parity.css", "utf8");
    expect(css).toMatch(/\.mock-viewer \.btn-link \{[^}]*background: none;[^}]*font-size: 12px;/);
  });
});
