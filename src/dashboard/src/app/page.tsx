export default function LandingPage() {
  return (
    <main className="flex min-h-screen items-center justify-center p-8">
      <section className="text-center">
        <h1 className="text-5xl font-medium tracking-tight" style={{ color: "var(--color-primary)" }}>
          agent-smith
        </h1>
        <p className="mt-4 text-lg" style={{ color: "var(--color-body)" }}>
          Dashboard bootstrap — toolchain online.
        </p>
        <p className="mt-2 text-sm" style={{ color: "var(--color-body-mid)" }}>
          Job-Viewer ships in p0169a.
        </p>
      </section>
    </main>
  );
}
