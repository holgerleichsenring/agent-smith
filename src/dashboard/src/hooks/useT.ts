"use client";

import { useSyncExternalStore } from "react";
import { currentLanguage, subscribeLanguage } from "@/lib/i18n/language";
import { translator, type Language, type T } from "@/lib/i18n/translate";

// 2026-11-22-5485a: the prerendered shell has no storage and no browser language, so the server snapshot is English;
// the page switches once it hydrates.

export function useLanguage(): Language {
  return useSyncExternalStore(subscribeLanguage, currentLanguage, () => "en");
}

/** The dictionary lookup in the language in force. */
export function useT(): T {
  return translator(useLanguage());
}
