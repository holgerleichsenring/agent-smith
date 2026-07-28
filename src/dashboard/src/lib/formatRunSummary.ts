// p0387: rows persisted BEFORE the backend humanizer existed still carry a raw
// provider error body as their summary — e.g. the Anthropic shape
// {"type":"error","error":{"message":...},"request_id":...} verbatim from
// ex.Message. Render fallback: extract the inner error.message; any summary
// that is not such a payload passes through unchanged. Pure module, no React.

export function formatRunSummary(summary: string): string {
  const message = extractProviderErrorMessage(summary);
  return message ?? summary;
}

/** The payload's error.message, or null when the text carries no such payload. */
function extractProviderErrorMessage(text: string): string | null {
  const start = text.indexOf("{");
  const end = text.lastIndexOf("}");
  if (start < 0 || end <= start) return null;
  try {
    const raw = JSON.parse(text.slice(start, end + 1)) as unknown;
    if (typeof raw !== "object" || raw === null) return null;
    const error = (raw as Record<string, unknown>).error;
    if (typeof error !== "object" || error === null) return null;
    const message = (error as Record<string, unknown>).message;
    return typeof message === "string" && message.trim() !== "" ? message : null;
  } catch {
    // Not JSON at all — prose that merely contains braces.
    return null;
  }
}
