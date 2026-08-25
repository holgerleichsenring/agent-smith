// 2026-08-25-8c97: which build produced the bundle this browser is running.
//
// Next.js inlines a NEXT_PUBLIC_* variable at BUILD time, which is exactly the property
// this needs: the value travels inside the JavaScript the browser downloaded and stays
// true after the pod that served it has been replaced. A runtime env-var would report the
// build of whichever pod answered last, which is the other half of the comparison, not
// this one.
//
// The literal `process.env.NEXT_PUBLIC_*` form is required — Next.js substitutes the text,
// so a computed key would not be replaced and the bundle would ship an undefined.
export const BUILD_REVISION = (process.env.NEXT_PUBLIC_BUILD_REVISION ?? "").trim();

export const RELEASE_VERSION = (process.env.NEXT_PUBLIC_RELEASE_VERSION ?? "").trim();

/**
 * The subsystem a build-difference finding names. The same string as the server's
 * StartupSubsystems.Build — a wire value, like the severity words next to it.
 */
export const BUILD_SUBSYSTEM = "build";
