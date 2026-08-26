// 2026-08-25-39ab: the ONE place a fetch response becomes a typed object.
//
// Every *Api module used to compose its own base URL and assert its own body
// into its own interface with a bare `as`. A payload the dashboard did not
// recognise therefore failed in thirteen different ways, and a shape change was
// thirteen edits and a hunt. The base URL, the status check and the JSON read
// live here now.
//
// This does NOT make the types true — nothing here validates a body against its
// interface. What it does is give that check somewhere to go once the REST
// contract is generated from the server: one function, one edit.

import { currentAccessToken } from "@/lib/auth/session";

/** The API origin every request is composed against. Empty means same-origin. */
export const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export function apiUrl(path: string): string {
  return `${API_BASE}${path}`;
}

/**
 * A response the client asked for and could not use — either the server refused
 * it, or the body was not something this client could read. The path is part of
 * the message because "HTTP 500" alone never said which surface went down.
 */
export class ApiResponseError extends Error {
  readonly path: string;
  readonly status: number;

  constructor(path: string, status: number, detail: string) {
    super(`${path}: ${detail}`);
    this.name = "ApiResponseError";
    this.path = path;
    this.status = status;
  }
}

/** What a caller must do about a refusal. Sign in, or hold a permission they
 *  do not — and those are the only two answers the server gives. */
export type RefusalKind = "sign-in" | "permission";

/**
 * 2026-08-25-4530: a refusal the caller can act on. Deliberately NOT an
 * ApiResponseError: a 401 is not a fault of this installation, it is the state
 * of the person reading it, and rendering it as "HTTP 401" told an operator
 * nothing about the one thing they had to do. It carries what the server named
 * so a surface can render the state instead of the status code.
 */
export class ApiRefusal extends Error {
  readonly path: string;
  readonly status: number;
  readonly kind: RefusalKind;
  /** The permissions p0503b's forbid body named. Empty when it named none. */
  readonly missingPermissions: readonly string[];

  constructor(path: string, status: number, kind: RefusalKind, missing: readonly string[]) {
    super(`${path}: ${sentenceFor(kind, missing)}`);
    this.name = "ApiRefusal";
    this.path = path;
    this.status = status;
    this.kind = kind;
    this.missingPermissions = missing;
  }
}

/**
 * The refusal a response carries, or null when it carries none. This RESOLVES
 * rather than throws — that is the whole point: a state the application renders
 * cannot arrive as an exception raised inside somebody's render.
 */
export async function refusalOf(res: Response, path = "the API"): Promise<ApiRefusal | null> {
  if (res.status === 401) return new ApiRefusal(path, 401, "sign-in", []);
  if (res.status === 403) return new ApiRefusal(path, 403, "permission", await named(res, path));
  return null;
}

/**
 * The refusal a caught failure carries, or null when it is an ordinary one.
 *
 * 2026-08-25-3277: this is why a loader holds the value it CAUGHT rather than
 * the message it read off it. The refusal survives the throw — a loader that
 * stores `err.message` loses the type at the last step, and the panel that
 * would have offered a sign-in button renders a sentence instead. Reading the
 * message at render time costs a loader nothing and leaves the type intact for
 * the surfaces with room for the action that resolves it.
 */
export function refusalIn(thrown: unknown): ApiRefusal | null {
  return thrown instanceof ApiRefusal ? thrown : null;
}

/** How a non-ok response fails: a refusal where there is one, a fault otherwise. */
export async function refused(res: Response, path: string): Promise<Error> {
  return (await refusalOf(res, path)) ?? new ApiResponseError(path, res.status, `HTTP ${res.status}`);
}

// p0503b writes the permission names into the forbid body precisely because
// ASP.NET's own path writes nothing. A proxy's 403 page carries no such body,
// and a refusal nobody can read is still a refusal — it just names less.
async function named(res: Response, path: string): Promise<string[]> {
  try {
    const body = (await res.json()) as { missingPermissions?: unknown } | null;
    const missing = body?.missingPermissions;
    return Array.isArray(missing) ? missing.filter((p): p is string => typeof p === "string") : [];
  } catch (cause) {
    console.debug(`${path}: the refusal named no permissions this client could read`, cause);
    return [];
  }
}

function sentenceFor(kind: RefusalKind, missing: readonly string[]): string {
  if (kind === "sign-in") return "you are signed out — this installation asks for a sign-in";
  if (missing.length === 0) return "the server refused this and named no permission";
  return `you are missing ${missing.length === 1 ? "the permission" : "the permissions"} ${missing.join(", ")}`;
}

/** One fetch, composed against the API origin. No status check — the caller
 *  that branches on 404/409 needs the response, not an exception. */
export async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  return fetch(apiUrl(path), await authorized(init));
}

// 2026-08-25-2de1: this being the ONE outgoing point is what makes the bearer one
// line rather than fifteen. No token held returns the caller's init untouched, so
// an installation with no authority sends the request it has always sent, header
// for header.
async function authorized(init?: RequestInit): Promise<RequestInit | undefined> {
  const token = await currentAccessToken();
  if (!token) return init;
  const headers = new Headers(init?.headers);
  headers.set("Authorization", `Bearer ${token}`);
  return { ...init, headers };
}

/**
 * Turn a response into the object the caller expects, or fail with a message
 * that names the surface and what arrived instead. A proxy's HTML error page,
 * an empty body, a truncated stream — all land here, once.
 */
export async function readJson<T>(res: Response, path = "the API"): Promise<T> {
  // 2026-08-25-4530: a refusal leaves here as a refusal. Everything else — a
  // 500, a 502, a gateway that answered HTML — fails exactly as it did before.
  if (!res.ok) throw await refused(res, path);
  try {
    return (await res.json()) as T;
  } catch (cause) {
    throw new ApiResponseError(path, res.status, unreadable(res, cause));
  }
}

/** GET a path and read its body — what most read clients do and nothing more. */
export async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  return readJson<T>(await apiFetch(path, { signal }), path);
}

/** Send a body and read the answer — the write-side twin of getJson. */
export async function sendJson<T>(
  method: string,
  path: string,
  body: unknown,
  signal?: AbortSignal,
): Promise<T> {
  const res = await apiFetch(path, {
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal,
  });
  return readJson<T>(res, path);
}

function unreadable(res: Response, cause: unknown): string {
  const type = res.headers?.get?.("content-type") ?? null;
  const reason = cause instanceof Error ? cause.message : String(cause);
  return `the server answered HTTP ${res.status} with a body this client cannot read as JSON`
    + `${type ? ` (content-type ${type})` : ""} — ${reason}`;
}
