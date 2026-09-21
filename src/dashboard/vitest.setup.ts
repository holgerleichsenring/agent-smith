import "@testing-library/jest-dom/vitest";

// 2026-09-20-4b0ae: jsdom resolves no layout and therefore implements no scrollIntoView, so
// the first component that calls one throws inside every test that renders it — including
// tests that say nothing about scrolling and were green the day before. The stub belongs to
// the environment rather than to the file that happens to need it first, and a test that
// wants to WATCH a scroll spies on this property.
if (typeof Element.prototype.scrollIntoView !== "function") {
  Element.prototype.scrollIntoView = function scrollIntoView() {};
}
