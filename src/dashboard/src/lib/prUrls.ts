// p0372: PR references embedded in outcome prose. A FAILED-outcome summary
// often carries the draft-PR URL as raw text; these helpers pull the URLs out
// so they render as the one PrButton, and strip them from the prose so the
// same reference never shows twice (dead text next to the live button).
// p0502: and where a URL ENDS is decided here for the whole dashboard. The run
// detail pane used to answer that question itself with [^\s]+, which swallowed the
// comma of a comma-joined list into every href but the last; two vocabularies for
// one question is what let them disagree.

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

/** One segment of prose: either a URL to link, or the text between them. */
export type TextSegment = { value: string; isUrl: boolean };

/**
 * Splits prose into alternating text and URL segments, ending each URL where
 * extractUrls would. Punctuation trimmed off a URL goes back into the following
 * TEXT segment rather than being dropped — the separator is the message's own
 * wording, so concatenating every segment reproduces the input exactly.
 */
export function splitOnUrls(text: string): TextSegment[] {
  const segments: TextSegment[] = [];
  let cursor = 0;
  for (const match of text.matchAll(URL_PATTERN)) {
    const url = trimTrailingPunctuation(match[0]);
    const start = match.index ?? 0;
    if (start > cursor) segments.push({ value: text.slice(cursor, start), isUrl: false });
    segments.push({ value: url, isUrl: true });
    cursor = start + url.length;
  }
  if (cursor < text.length) segments.push({ value: text.slice(cursor), isUrl: false });
  return segments;
}
