import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Why Tests Miss Bugs | The Green Build Fallacy",
  description:
    "Tests pass but bugs still reach production. Learn the categories of risk that escape test suites and why a green build is not the same as safe code.",
  alternates: { canonical: "/why-tests-miss-bugs" },
};

const categories = [
  {
    title: "Behavioral drift",
    body: "A guard clause or fallback branch is quietly removed. The existing tests never exercised that path, so they all still pass. The behavior of the system changed, but no assertion noticed. These are among the hardest bugs to debug in production because the code looks correct.",
    example: "A null check before a database write is deleted. No test covers the null path. Green build, silent data corruption in production on the first null input.",
  },
  {
    title: "Implicit contract changes",
    body: "A public method's parameter type changes from int to long, or an enum value is removed. The compiler is happy if all call sites were updated. But serialized data, external consumers, or stored procedures that referenced the old shape break silently.",
    example: "An API response field changes from string to array. The serialization layer compiles. Consumer services fail at runtime with deserialization errors.",
  },
  {
    title: "Missing null and edge-case guards",
    body: "New code paths are added that handle the happy path. The unhappy paths - null inputs, empty collections, boundary values - are not considered. Tests that call the new code use valid inputs and pass. Production surfaces the edge case.",
    example: "A refactor that adds a new code path forgets to handle an empty list. Every test passes a populated list. Production sends the first empty list and throws.",
  },
  {
    title: "Config and environment side effects",
    body: "A change reads a new environment variable, changes a default timeout, or adds a dependency on a service URL. Unit tests mock the environment. Integration tests may not exercise the exact configuration surface. The gap is in the setup, not the logic.",
    example: "A hardcoded connection string is added for local dev. Tests pass against a test database. Production reads the hardcoded value and ignores the environment variable.",
  },
  {
    title: "Async and concurrency changes",
    body: "An async void method is introduced, a .Result call blocks a thread, or shared mutable state is accessed without synchronization. Unit tests run sequentially in a controlled environment. Race conditions and deadlocks only appear under real concurrency load.",
    example: "A .Result call blocks the thread pool under high load. Single-threaded tests pass instantly. Production intermittently deadlocks under traffic.",
  },
  {
    title: "Dependency and schema drift",
    body: "A dependency update changes an API, a database migration removes a column, or a serialization attribute is removed. Tests pin to a specific version or mock the dependency. The real integration only surfaces on deploy.",
    example: "A JSON property is renamed in a dependent library update. Tests mock the response. Production fails to deserialize real API responses.",
  },
];

export default function WhyTestsMissBugsPage() {
  return (
    <>
      <Header />
      <main className="min-h-screen bg-background pt-24">

        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">The problem</p>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Why tests miss bugs
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              A green build means your tests passed. It does not mean your code is safe.
              Tests are written to verify what developers expected. They cannot verify
              what developers forgot.
            </p>
          </div>

          {/* The Green Build Fallacy */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">The Green Build Fallacy</h2>
            <p className="text-muted-foreground leading-relaxed">
              Most engineering teams treat a passing CI pipeline as a safety signal. In practice,
              test suites verify a snapshot of intended behavior written at a specific point in time.
              Every change to the codebase creates a new surface area that existing tests may
              not cover.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The result is a growing gap between "the tests pass" and "the system behaves correctly
              under all inputs." This gap is where production incidents live. It is not a failure of
              the test suite - it is the inherent limit of testing as a verification strategy.
            </p>
            <div className="rounded-lg border border-amber-500/20 bg-amber-500/5 p-5">
              <p className="text-sm text-amber-400 font-medium">
                Studies consistently show that 40-60% of production incidents are caused by
                changes that passed full CI. The tests were not wrong. They were incomplete.
              </p>
            </div>
          </section>

          {/* Categories */}
          <section className="space-y-6">
            <h2 className="text-2xl font-bold tracking-tight">6 categories of bugs that escape test suites</h2>
            <p className="text-muted-foreground">
              These are not exotic edge cases. They are the most common root causes behind
              production regressions in .NET codebases.
            </p>
            <div className="space-y-4">
              {categories.map((cat, i) => (
                <div key={cat.title} className="rounded-xl border border-border bg-card overflow-hidden">
                  <div className="flex items-start gap-4 p-5">
                    <span className="shrink-0 mt-0.5 text-xs font-mono text-muted-foreground/50 w-5">
                      {String(i + 1).padStart(2, "0")}
                    </span>
                    <div className="space-y-3 min-w-0">
                      <h3 className="font-semibold text-foreground">{cat.title}</h3>
                      <p className="text-sm text-muted-foreground leading-relaxed">{cat.body}</p>
                      <div className="rounded-md bg-background/50 border border-border px-4 py-3">
                        <p className="text-xs font-semibold text-muted-foreground/60 uppercase tracking-widest mb-1">Example</p>
                        <p className="text-xs text-muted-foreground leading-relaxed">{cat.example}</p>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* Bridge */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">How GauntletCI bridges the gap</h2>
            <p className="text-muted-foreground leading-relaxed">
              GauntletCI does not replace tests. It analyzes the diff itself - the lines you added
              or removed - and flags the structural patterns that tests routinely miss. It runs
              before the commit is created, when the fix costs nothing and the context is freshest.
            </p>
            <div className="grid sm:grid-cols-3 gap-4">
              {[
                { label: "Removed logic detection", detail: "Flags removed guard clauses, null checks, and fallback branches that are not covered by updated tests." },
                { label: "API contract analysis", detail: "Detects public method signature changes and removed serialization attributes that break callers at runtime." },
                { label: "Async and concurrency rules", detail: "Catches async void, blocking .Result calls, and shared state mutations before they cause deadlocks in production." },
              ].map((item) => (
                <div key={item.label} className="rounded-lg border border-border bg-card/50 p-4">
                  <p className="text-sm font-semibold text-cyan-400 mb-1.5">{item.label}</p>
                  <p className="text-xs text-muted-foreground leading-relaxed">{item.detail}</p>
                </div>
              ))}
            </div>
          </section>

          {/* CTAs */}
          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/docs"
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
            >
              Try GauntletCI free
            </Link>
            <Link
              href="/docs/rules"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              See the 30 detection rules
            </Link>
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}
