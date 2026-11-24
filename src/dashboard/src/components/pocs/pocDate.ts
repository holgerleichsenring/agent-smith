import type { Language } from "@/lib/i18n/translate";

// 2026-11-23-c81ee: a PoC's expiry and deletion dates read in the UI language — "7 December 2026", "7. Dezember 2026".

export function pocDate(iso: string, language: Language): string {
  const at = new Date(iso);
  if (Number.isNaN(at.getTime())) return iso;
  return at.toLocaleDateString(language === "de" ? "de-DE" : "en-GB", { day: "numeric", month: "long", year: "numeric" });
}
