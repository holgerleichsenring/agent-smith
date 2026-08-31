// 2026-08-28-0f46: the hidden frame a silent sign-in loads is this whole
// application. The library points that frame at the ordinary reply URL, and
// this phase deliberately leaves it there — the alternatives each cost a second
// redirect URI registered on the authority, which is an operator change.
//
// So the frame boots the dashboard, lives about a second and is then removed.
// What it must NOT do is the part worth naming in one place: it must not
// navigate, and it must not open a second live connection to a hub the tab
// above it is already connected to.

/**
 * True when this document is the hidden frame of a silent sign-in — an embedded
 * window whose URL carries an authority's answer to a request it did not make
 * for itself. False on a server, and false in the tab that owns the sign-in.
 */
export function isSilentReturnFrame(): boolean {
  if (typeof window === "undefined") return false;
  if (window.parent === window.self) return false;
  const answer = new URLSearchParams(window.location.search);
  return answer.has("state") && (answer.has("code") || answer.has("error"));
}
