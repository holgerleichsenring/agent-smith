import type { Language } from "./translate";

// 2026-11-22-5485a: which language the dashboard speaks, per browser. The operator's choice is kept in localStorage;
// with none, the browser's language decides — German for de*, English for everything else. Blocked storage leaves the
// choice for this page only. One value for the whole page, so a switch re-renders every component that reads it.

const KEY = "agentsmith.language";

let current: Language | null = null;
const listeners = new Set<() => void>();

/** The browser's own language as one the dashboard speaks. */
export function browserLanguage(): Language {
  const said = typeof navigator === "undefined" ? "" : navigator.language ?? "";
  return said.toLowerCase().startsWith("de") ? "de" : "en";
}

function stored(): Language | null {
  try {
    const value = window.localStorage.getItem(KEY);
    return value === "de" || value === "en" ? value : null;
  } catch {
    return null;
  }
}

/** The language in force: the stored choice, else the browser's. */
export function currentLanguage(): Language {
  current ??= stored() ?? browserLanguage();
  return current;
}

/** The operator chose `language`: remembered for this browser, and every reader re-renders. */
export function chooseLanguage(language: Language): void {
  current = language;
  try {
    window.localStorage.setItem(KEY, language);
  } catch {
    /* kept for this page only */
  }
  listeners.forEach((listener) => listener());
}

export function subscribeLanguage(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Tests: start from `language`, or re-read storage and the browser on the next read when none is given. */
export function resetLanguage(language?: Language): void {
  current = language ?? null;
  listeners.forEach((listener) => listener());
}
