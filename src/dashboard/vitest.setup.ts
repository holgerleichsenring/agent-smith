import "@testing-library/jest-dom/vitest";

// 2026-09-20-4b0ae: jsdom resolves no layout and therefore implements no scrollIntoView, so
// the first component that calls one throws inside every test that renders it — including
// tests that say nothing about scrolling and were green the day before. The stub belongs to
// the environment rather than to the file that happens to need it first, and a test that
// wants to WATCH a scroll spies on this property.
if (typeof Element.prototype.scrollIntoView !== "function") {
  Element.prototype.scrollIntoView = function scrollIntoView() {};
}

// 2026-11-22-9719c: the caller's identity is one read shared across the page; each test starts without it. The reset
// lives beside the read, which no test mocks.
import { beforeEach } from "vitest";
beforeEach(async () => {
  const shared = await import("@/lib/sharedIdentityRead");
  (shared as { resetSharedCallerIdentity?: () => void }).resetSharedCallerIdentity?.();
});

// 2026-11-22-5485a: the tests read the English text, whatever the machine's language or a choice an earlier test
// stored; a test that wants German chooses it.
beforeEach(async () => {
  try {
    window.localStorage.removeItem("agentsmith.language");
  } catch {
    /* no storage in this environment */
  }
  const language = await import("@/lib/i18n/language");
  language.resetLanguage("en");
});
