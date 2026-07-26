// p0372: PR references embedded in outcome prose. A FAILED-outcome summary
// often carries the draft-PR URL as raw text; these helpers pull the URLs out
// so they render as the one PrButton, and strip them from the prose so the
// same reference never shows twice (dead text next to the live button).

const URL_PATTERN = /https?:\/\/[^\s)\]>"']+/g;

/** Every http(s) URL in the text, de-duplicated, trailing punctuation trimmed. */
export function extractUrls(text: string | null | undefined): string[] {
  if (!text) return [];
  const matches = text.match(URL_PATTERN) ?? [];
  return [...new Set(matches.map(trimTrailingPunctuation))];
}

/** The text with every URL removed and whitespace re-collapsed. */
export function stripUrls(text: string): string {
  return text.replace(URL_PATTERN, "").replace(/\s{2,}/g, " ").trim();
}

function trimTrailingPunctuation(url: string): string {
  return url.replace(/[.,;:!?]+$/, "");
}
