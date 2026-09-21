// 2026-09-21-291b: the one bit that keeps an automatic sign-in from becoming a
// redirect loop. AuthSession's header rejects an interactive redirect on boot for
// exactly two reasons — a loop, and taking over a dashboard whose server enforces
// nothing. The second is answered by the server, which now reports `enforced`.
// This answers the first.
//
// It is written BEFORE the redirect leaves, not after it returns: a redirect that
// fails at the authority never comes back to write anything, and that is precisely
// the case a loop is made of.
//
// sessionStorage, not localStorage: it is a fact about THIS tab's attempt. A second
// tab of the same dashboard has made no attempt and must be free to make one. That
// a duplicated tab inherits the mark is the safe direction to be wrong in — it
// offers a button instead of navigating on its own.
//
// It is also what a deliberate sign-out leaves behind, so the gate offers the door
// rather than walking the person back through it.

const KEY = "agentsmith.signin.authority-reached";

/** Test-visible so the gate's own tests can state the tab's starting position. */
export function theAuthorityWasReached(): boolean {
  return read()?.getItem(KEY) === "1";
}

export function rememberTheAuthorityWasReached(): void {
  read()?.setItem(KEY, "1");
}

/** A token landed, so the next sign-out or expiry starts from a clean tab. */
export function forgetTheAuthorityWasReached(): void {
  read()?.removeItem(KEY);
}

// A browser with site data blocked throws on the ACCESSOR, before any method is
// called. Storage nobody can read is a tab that has reached nobody, which keeps
// the gate offering its button rather than failing the render around it.
function read(): Storage | null {
  try {
    return typeof window === "undefined" ? null : window.sessionStorage;
  } catch (cause) {
    console.debug("this browser does not let the dashboard remember its sign-in attempt", cause);
    return null;
  }
}
