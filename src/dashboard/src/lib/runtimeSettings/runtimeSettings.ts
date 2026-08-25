// 2026-08-25-21ae: the settings an operator sets per installation, read from a
// document the image entrypoint writes at start rather than from a NEXT_PUBLIC_*
// variable Next.js inlined when the bundle was compiled. A value in the bundle
// costs a rebuild to change, which is not what a setting is.
//
// NOTHING SECRET IS IN THIS DOCUMENT. It is served to any browser that asks. An
// OAuth public client's identifier and its authority are public values; a client
// secret, a token or a connection string are not, and nothing here could tell
// the difference.
//
// AN ABSENT DOCUMENT IS THE OFF STATE, NOT AN ERROR. `next dev` runs no
// entrypoint, so a developer's tree has no document at all — a 404, a body this
// client cannot read, and a network that never answered all resolve to every
// setting at its default, which is what every installation does today.

/** Same-origin, served from the static root the entrypoint writes into. */
export const RUNTIME_SETTINGS_PATH = "/runtime-settings.json";

/** What the browser needs to reach an authority. No secret belongs in it. */
export interface RuntimeAuthSettings {
  /** The OIDC issuer. Empty means no authority is configured. */
  authority: string;
  /** The public client the browser identifies itself as. */
  clientId: string;
  /** The audience a token is requested for. */
  audience: string;
  /** Space-separated scopes, as the authority expects them on the wire. */
  scopes: string;
  /** Where the authority returns to, relative to this dashboard's origin. */
  redirectPath: string;
}

export interface RuntimeSettings {
  auth: RuntimeAuthSettings;
}

// The redirect path is the one field with a non-empty default: it names a route
// inside this dashboard rather than anything about the installation, and empty
// would be a broken value rather than an unconfigured one. The entrypoint
// substitutes the same default, so a document written with nothing set and no
// document at all are the same settings.
export const DEFAULT_RUNTIME_SETTINGS: RuntimeSettings = {
  auth: {
    authority: "",
    clientId: "",
    audience: "",
    scopes: "",
    redirectPath: "/signin-callback",
  },
};

let boot: Promise<RuntimeSettings> | null = null;

/**
 * The resolved settings for this boot. Fetched once — two reads would give the
 * application two answers, and a window in which one component believes a
 * setting is on and another does not.
 */
export function loadRuntimeSettings(): Promise<RuntimeSettings> {
  boot ??= read();
  return boot;
}

// Not composed through apiResponse: this is not the API. It is a static file on
// the dashboard's own origin, and composing it against NEXT_PUBLIC_API_BASE_URL
// would send it wherever the backend happens to live.
async function read(): Promise<RuntimeSettings> {
  try {
    // no-store, or a browser that has the document keeps answering from the old
    // installation's settings after the pod that served it is gone. Affordable
    // exactly because it happens once per boot.
    const res = await fetch(RUNTIME_SETTINGS_PATH, { cache: "no-store" });
    if (!res.ok) return DEFAULT_RUNTIME_SETTINGS;
    return resolve(await res.json());
  } catch (cause) {
    // A 404 never lands here — it is the OFF state and returns above. This is a
    // document that exists and could not be read, which an operator wants named.
    console.warn(
      `${RUNTIME_SETTINGS_PATH} could not be read — every setting keeps its default`,
      cause,
    );
    return DEFAULT_RUNTIME_SETTINGS;
  }
}

function resolve(body: unknown): RuntimeSettings {
  const auth = (body as { auth?: Record<string, unknown> } | null)?.auth;
  const fallback = DEFAULT_RUNTIME_SETTINGS.auth;
  return {
    auth: {
      authority: text(auth?.authority, fallback.authority),
      clientId: text(auth?.clientId, fallback.clientId),
      audience: text(auth?.audience, fallback.audience),
      scopes: text(auth?.scopes, fallback.scopes),
      redirectPath: text(auth?.redirectPath, fallback.redirectPath),
    },
  };
}

// A field an older entrypoint never wrote is absent, not wrong — it takes its
// default rather than making the whole document unreadable.
function text(value: unknown, fallback: string): string {
  return typeof value === "string" ? value : fallback;
}
