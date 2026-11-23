import type { Language } from "./translate";

// 2026-11-23-5669a: dates and times are written in the chosen language, not the browser's. English is en-GB, so a
// time reads 14:05 in both languages.

const LOCALES: Record<Language, string> = { en: "en-GB", de: "de-DE" };

/** The locale every date, time and number of `language` is formatted with. */
export function dateLocale(language: Language): string {
  return LOCALES[language];
}
