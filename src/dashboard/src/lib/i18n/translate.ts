import { messages, type MessageKey } from "./messages";

// 2026-11-22-5485a: the lookup a component renders with. A text carries {name} placeholders filled from the params;
// a plural key picks its one or other form by params.count, the same rule in both languages.

export type Language = "en" | "de";

export const LANGUAGES: readonly Language[] = ["en", "de"];

export type Params = Record<string, string | number>;

type Entry = (typeof messages)[MessageKey];

/** The keys whose text has a one and an other form. */
export type PluralKey = { [K in MessageKey]: (typeof messages)[K]["en"] extends string ? never : K }[MessageKey];

/** The keys with one text. */
export type TextKey = Exclude<MessageKey, PluralKey>;

export interface T {
  (key: TextKey, params?: Params): string;
  (key: PluralKey, params: Params & { count: number }): string;
}

/** `text` with every {name} replaced by params[name]; a placeholder with no param stays as written. */
export function fill(text: string, params?: Params): string {
  if (!params) return text;
  return text.replace(/\{(\w+)\}/g, (whole, name: string) => (name in params ? String(params[name]) : whole));
}

function lookup(language: Language, key: MessageKey, params?: Params): string {
  const form = (messages[key] as Entry)[language];
  if (typeof form === "string") return fill(form, params);
  return fill(params?.count === 1 ? form.one : form.other, params);
}

const translators: Record<Language, T> = {
  en: ((key: MessageKey, params?: Params) => lookup("en", key, params)) as T,
  de: ((key: MessageKey, params?: Params) => lookup("de", key, params)) as T,
};

/** The lookup for `language`; the same function each time, so it is safe in a dependency list. */
export function translator(language: Language): T {
  return translators[language];
}
