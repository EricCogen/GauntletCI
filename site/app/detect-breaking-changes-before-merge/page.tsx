import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Detect Breaking Changes Before Merge | GauntletCI",
  description:
    "Breaking changes in .NET code are often invisible at compile time. Learn the patterns that break callers at runtime and how to catch them before the PR merges.",
  alternates: { canonical: "/detect-breaking-changes-before-merge" },
  openGraph: { images: [{ url: '/og/detect-breaking-changes-before-merge.png', width: 1200, height: 630 }] },
};

const patterns = [
  {
    category: "Public API surface",
    items: [
      { name: "Removed public method", detail: "Callers that compile today fail at runtime after deploy if the method existed in a referenced assembly. GCI0004 flags this as high severity." },
      { name: "Changed method signature", detail: "Adding required parameters or changing parameter types breaks callers that used the old signature. Callers compiled against the old signature get MissingMethodException at runtime." },
      { name: "Removed interface member", detail: "Classes that implement the interface still compile. Classes in other assemblies that call the removed member fail at runtime with MissingMethodException." },
      { name: "Sealed class where previously unsealed", detail: "Callers that subclass the type at runtime (including mocking frameworks and dependency injection containers) fail with TypeLoadException." },
      { name: "Removed public property", detail: "Properties serialized to JSON, bound in XAML data bindings, or read via reflection by frameworks such as AutoMapper fail silently or throw at runtime." },
      { name: "Changed return type", detail: "Changing a return type is a binary breaking change even if both types share a common interface. Callers compiled against the old signature receive MissingMethodException." },
    ],
  },
  {
    category: "Serialization contracts",
    items: [
      { name: "Removed [JsonPropertyName] attribute", detail: "The property is no longer mapped from its wire name. Existing payloads silently deserialize to null or default, producing bugs that tests rarely cover." },
      { name: "Renamed property without attribute", detail: "The serialized name changes. Previously stored or transmitted JSON fails to deserialize, causing silent data loss on read." },
      { name: "Changed property type", detail: "JSON deserializers throw or silently coerce. Strongly typed consumers fail at runtime, and the failure may not surface until a specific payload shape is encountered." },
      { name: "Removed [JsonIgnore] attribute", detail: "A previously hidden property is now serialized. Downstream consumers may reject the extra field or surface internal data unintentionally." },
      { name: "Changed enum serialization mode", detail: "Switching between integer and string enum serialization breaks stored payloads and any consumer that relied on the previous wire format." },
    ],
  },
  {
    category: "Dependency injection and service registration",
    items: [
      { name: "Removed service registration", detail: "Code that resolves the service at runtime receives null or throws InvalidOperationException. GCI0038 flags removal of AddSingleton, AddScoped, and AddTransient calls." },
      { name: "Changed constructor signature", detail: "DI containers that resolve by convention fail to construct the type at runtime if required parameters are added or types change." },
      { name: "Changed service lifetime", detail: "Scoped services injected into singletons produce runtime errors or subtle state-sharing bugs that are hard to reproduce in unit tests." },
      { name: "Replaced concrete registration with interface", detail: "Code that resolves the concrete type directly (common in integration tests and some framework integrations) fails with an unresolved dependency error at runtime." },
    ],
  },
  {
    category: "Database and storage",
    items: [
      { name: "Removed EF Core entity property", detail: "Migrations not deployed before the application update cause runtime query failures when EF tries to map columns that no longer exist in the model." },
      { name: "Changed column type without migration", detail: "Data read from the old schema fails to map to the new type at runtime, producing either a conversion exception or silent data truncation." },
      { name: "Removed database index", detail: "Query plans that relied on the index degrade to full table scans. Throughput drops appear hours after deploy as query volumes increase. GCI0021 flags this pattern." },
      { name: "Renamed entity class or table", detail: "EF Core uses the class name as the default table name. Renaming without a [Table] attribute causes runtime failures against the existing database schema." },
    ],
  },
  {
    category: "Thread safety and concurrency",
    items: [
      { name: "async void method introduced", detail: "async void methods swallow exceptions silently. Unhandled exceptions escape the calling context and crash the process in ASP.NET Core. GCI0016 flags every non-event-handler async void." },
      { name: "Blocking async call (.Result or .Wait())", detail: "Calling .Result or .Wait() on a Task in an async context causes deadlocks in ASP.NET Core and Blazor applications. GCI0016 flags these patterns in added lines." },
      { name: "Static mutable field introduced", detail: "Static fields shared across requests in a web application produce race conditions that are nearly impossible to reproduce in unit tests." },
      { name: "lock(this) introduced", detail: "Locking on this exposes the lock object to external callers, creating potential for deadlocks from code outside the class." },
      { name: "Thread.Sleep in async context", detail: "Thread.Sleep blocks the thread pool thread, degrading throughput under load. In async code the correct call is await Task.Delay." },
    ],
  },
];

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "Detect Breaking Changes Before Merge",
  "description": "Breaking changes in .NET code are often invisible at compile time. Learn the patterns that break callers at runtime and how to detect them before the PR merges.",
  "url": "https://gauntletci.com/detect-breaking-changes-before-merge",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function DetectBreakingChangesPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">

        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">The problem</p>
              <Link href="/articles" className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors">← All articles</Link>
            </div>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Detect breaking changes before merge
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              Breaking changes in .NET code are often invisible at compile time. The compiler
              says green. The tests pass. Production fails the moment the first real request hits
              the changed code path. This article explains why that gap exists, what the most
              common breaking change patterns look like, and how GauntletCI closes the gap
              before a commit ever reaches the repository.
            </p>
            <div className="flex items-center gap-2 pt-1">
              <span className="text-sm font-medium text-foreground">Eric Cogen</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">Founder, GauntletCI</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <time className="text-sm text-muted-foreground" dateTime="2026-04-20">April 20, 2026</time>
            </div>
            <nav className="flex items-center justify-between pt-2 text-sm border-t border-border/50">
              <Link href="/what-is-diff-based-analysis" className="flex items-center gap-1 text-muted-foreground hover:text-cyan-400 transition-colors">
                <span aria-hidden="true">‹</span> What Is Diff-Based Analysis?
              </Link>
              <span />
            </nav>
          </div>

          {/* Why the compiler is not enough */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">Why the compiler is not enough</h2>
            <figure className="float-right ml-6 mb-4 w-[19%] rounded-lg overflow-hidden border border-border">
              <img
                src="/articles/detect-breaking-changes-before-merge-hero.png"
                alt="Pipeline diagram: change to compiler ok to tests ok to deploy to runtime fail, with brackets showing compiler checks source and runtime checks binaries"
                width={520}
                height={210}
                className="w-full h-auto"
              />
            </figure>
            <p className="text-muted-foreground leading-relaxed">
              The .NET compiler catches type errors and missing references within a project or
              solution. It does not verify runtime contracts. A method signature change may
              compile successfully if all call sites within the repository are updated, but
              external consumers, serialized payloads in databases or message queues, and
              dynamically resolved services have no compile-time check at all.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The compiler operates on source. The runtime operates on binaries. When a library
              assembly is updated and deployed without recompiling every consumer, the compiler
              had no opportunity to raise an error for the consumer assemblies that still carry
              metadata pointing at the old method signature. The first runtime call to the
              changed or removed method produces a MissingMethodException or TypeLoadException
              that no amount of static analysis on the library alone could have predicted.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              These are not rare edge cases. They are the normal state of any system with more
              than one service, any persistence layer, or any public API surface. The compiler
              success guarantee is narrow. The runtime failure surface is wide.
            </p>
          </section>

          {/* Why semver does not prevent runtime breaks */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Why semantic versioning does not prevent runtime breaking changes</h2>
            <p className="text-muted-foreground leading-relaxed">
              Semantic versioning is a communication protocol, not an enforcement mechanism. A
              library author who increments the major version correctly has communicated that
              something breaking changed. The downstream consumer who has not yet updated their
              code still gets a runtime failure the moment they update the package reference
              without reading the changelog.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              More importantly, semver requires the library author to know which changes are
              breaking in the first place. Research by Dig and Johnson found that 80 percent of
              the 147 breaking API changes they studied across open source Java projects were
              caused by refactoring: changes the author considered routine cleanup rather than
              intentional API evolution{" "}
              <a href="#cite-1" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[1]</a>
              . The study examined open-source Java projects; no large-scale .NET-specific dataset
              substantially contradicts the proportion, though the precise rate likely varies by
              ecosystem and project type. A developer who renames a method during a refactor
              sprint is unlikely to reach for the changelog before committing. Semver provides
              no signal until after the commit is merged and the package is published.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The cost of discovering a breaking change at the consumer is higher than the cost
              of preventing it at the author. Hora et al. measured how downstream projects
              reacted when their library dependencies introduced breaking changes and found that
              many projects simply stopped updating the dependency, accumulating security and
              correctness debt rather than absorbing the migration cost{" "}
              <a href="#cite-2" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[2]</a>
              . The practical
              implication is that library authors who introduce unintentional breaking changes do
              not just cause immediate runtime failures; they cause long-term ecosystem
              fragmentation. Pre-commit detection converts this class of problem from an
              ecosystem-level event into a single-developer edit.
            </p>
          </section>

          {/* The multi-assembly problem */}
          <section className="space-y-6 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">The multi-assembly problem in .NET: why internal compilation success does not mean external compatibility</h2>
            <p className="text-muted-foreground leading-relaxed">
              In a .NET solution with multiple projects, the build system ensures every project
              within the solution compiles against the current source. When project A references
              project B, and a developer changes a public method in B, the build fails at project
              A immediately. This is the happy path. It is also the narrowest case.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The multi-assembly problem appears in three common real-world shapes:
            </p>
            <ul className="space-y-4 text-muted-foreground leading-relaxed list-none pl-0">
              <li className="flex gap-3">
                <span className="text-cyan-400 font-bold shrink-0">1.</span>
                <span>
                  <strong className="text-foreground">Published NuGet packages.</strong>{" "}
                  Once a library is published to NuGet, any consumer pinned to the previous
                  version will not recompile when the author pushes a new patch. Consumers
                  that accept floating version ranges and update the package will get a
                  binary that no longer matches the signatures their code was compiled against.
                  The result is a MissingMethodException or BadImageFormatException at the
                  first call into the changed surface.
                </span>
              </li>
              <li className="flex gap-3">
                <span className="text-cyan-400 font-bold shrink-0">2.</span>
                <span>
                  <strong className="text-foreground">Plugin and extension architectures.</strong>{" "}
                  Any host application that loads assemblies at runtime (MEF, Roslyn
                  analyzers, ASP.NET Core middleware loaded via reflection) cannot recompile
                  the plugins when the host API changes. The failure surface is entirely in the
                  runtime layer, and it typically surfaces in production long after the host
                  was updated.
                </span>
              </li>
              <li className="flex gap-3">
                <span className="text-cyan-400 font-bold shrink-0">3.</span>
                <span>
                  <strong className="text-foreground">Microservice contracts.</strong>{" "}
                  When two services communicate over HTTP or a message bus, each service is
                  compiled independently. Changing the shape of a shared data transfer object
                  in one service does not cause a compile error in the other, even if both
                  services live in the same repository. The failure appears at runtime when a
                  payload of the old shape arrives at a deserializer expecting the new shape,
                  often silently producing null-valued fields rather than a thrown exception.
                </span>
              </li>
            </ul>
            <p className="text-muted-foreground leading-relaxed">
              Microsoft tracks hundreds of compatibility breaks introduced across .NET framework
              versions, many of which involve scenarios where the compiler produced no error{" "}
              <a href="#cite-3" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[3]</a>
              . The documentation distinguishes binary incompatible changes from source
              incompatible changes: two distinct failure modes that require different
              mitigation strategies and that a build server running inside a single solution
              boundary cannot detect.
            </p>
          </section>

          {/* OSS history */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Breaking changes in .NET OSS history: real examples from well-known libraries</h2>
            <p className="text-muted-foreground leading-relaxed">
              The .NET ecosystem provides several well-documented examples of breaking changes
              that caused significant migration effort across the community. Each one follows the
              same pattern: correct compilation, runtime failure, delayed discovery.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              <strong className="text-foreground">Newtonsoft.Json to System.Text.Json.</strong>{" "}
              When Microsoft introduced System.Text.Json as the default serializer in ASP.NET
              Core 3.0, teams that migrated from Newtonsoft.Json encountered serialization
              contract breaks that were invisible at compile time. Newtonsoft serializes public
              fields by default; System.Text.Json does not. Newtonsoft performs case-insensitive
              property matching by default; System.Text.Json requires explicit configuration for
              the same behavior. Properties decorated with [JsonProperty] from Newtonsoft were
              silently ignored by the new serializer, causing wire format changes that surfaced
              only when stored payloads were read back or when downstream services that had not
              yet migrated received responses in the new format. None of these differences
              produced compile-time errors.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              <strong className="text-foreground">Entity Framework Core 6 to 7.</strong>{" "}
              EF Core 7 introduced breaking changes to how owned entity types are mapped to
              tables. Applications that relied on the previous behavior compiled without
              modification but produced different SQL at runtime. In some cases the queries
              returned wrong data rather than throwing an exception, which is the hardest
              failure mode to detect in automated testing. The EF Core team documented these
              changes in their migration guide, but developers who upgraded the package without
              reading the guide had no automated signal that behavior had changed.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              <strong className="text-foreground">AutoMapper major versions.</strong>{" "}
              AutoMapper removed its static API in version 9.0. Applications using the static
              API compiled against version 8.x would fail to compile against 9.x, but those
              that had already compiled and were running against 8.x would break only when the
              package version was updated and the host restarted. Teams running integration
              tests against the actual binary rather than recompiling from source would not
              catch the failure until deployment. The pattern repeats across the ecosystem
              because the toolchain provides no mechanism for flagging the risk at the point
              where the change is authored.
            </p>
          </section>

          {/* Code example */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">A breaking change the compiler cannot see</h2>
            <p className="text-muted-foreground leading-relaxed">
              The following example represents the class of failures GauntletCI is designed to
              catch. The library compiles without warnings. The application compiles without
              warnings. The failure appears at runtime.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Suppose a shared library ships this public API in version 1.0:
            </p>
            <div className="rounded-lg border border-border bg-card p-5 space-y-2">
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">MyLibrary.dll -- v1.0 (original)</p>
              <pre className="text-sm text-foreground font-mono leading-relaxed overflow-x-auto whitespace-pre">{`public class OrderProcessor
{
    public void Process(Order order, bool sendNotification)
    {
        // process the order, optionally send notification
    }
}`}</pre>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              A developer decides that notifications should always be sent and removes the
              parameter during a cleanup commit. The library is published as v1.1, a minor
              version bump, because the developer considers it a simplification rather than a
              breaking change:
            </p>
            <div className="rounded-lg border border-border bg-card p-5 space-y-2">
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">MyLibrary.dll -- v1.1 (after the cleanup)</p>
              <pre className="text-sm text-foreground font-mono leading-relaxed overflow-x-auto whitespace-pre">{`public class OrderProcessor
{
    // sendNotification removed -- notifications always sent now
    public void Process(Order order)
    {
        // process the order
    }
}`}</pre>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              Any application compiled against v1.0 that calls{" "}
              <code className="text-cyan-400 font-mono text-sm bg-card px-1.5 py-0.5 rounded border border-border">processor.Process(order, true)</code>{" "}
              will receive the following exception the first time that code path executes after
              the library is updated in production:
            </p>
            <div className="rounded-lg border border-red-900/40 bg-red-950/20 p-4">
              <pre className="text-sm text-red-400 font-mono leading-relaxed overflow-x-auto whitespace-pre">{`System.MissingMethodException: Method not found:
  'Void MyLibrary.OrderProcessor.Process(MyLibrary.Order, Boolean)'`}</pre>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              GauntletCI rule GCI0004 detects this at diff time. When it sees that a public
              method signature was removed with no matching overload added, it produces a
              high-severity finding before the commit is created. The developer learns about
              the break while still in their editor, not after the library is deployed and a
              consumer integration test suite begins to fail.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The correct fix is straightforward once the risk is visible: keep the old overload,
              mark it{" "}
              <code className="text-cyan-400 font-mono text-sm bg-card px-1.5 py-0.5 rounded border border-border">[Obsolete]</code>
              , and delegate to the new signature. Consumers continue to compile and run. The
              old overload can be removed in the next major version after consumers have had
              time to migrate. This is the standard .NET deprecation path, and it is the
              action GCI0004 suggests in its finding output.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This is the core insight behind{" "}
              <Link href="/what-is-diff-based-analysis" className="text-cyan-400 underline underline-offset-2 hover:text-cyan-300">
                diff-based analysis
              </Link>
              : by analyzing exactly the lines that are about to change, GauntletCI identifies
              the structural risk at the only moment when it is still cheap to address it.
            </p>
          </section>

          {/* Pattern categories */}
          <section className="space-y-8 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Breaking change patterns in .NET</h2>
            <p className="text-muted-foreground leading-relaxed">
              The patterns below represent the structural changes that most commonly cause
              runtime failures across .NET applications. Each pattern can be introduced by a
              well-intentioned commit (a refactor, a cleanup, a schema update) that passes
              all automated tests because the test suite was written against the post-change
              codebase and does not exercise the consumer boundary at the binary level.
            </p>
            {patterns.map((group) => (
              <div key={group.category} className="space-y-3">
                <h3 className="text-base font-semibold text-muted-foreground uppercase tracking-wide text-sm">{group.category}</h3>
                <div className="space-y-2">
                  {group.items.map((item) => (
                    <div key={item.name} className="flex gap-4 rounded-lg border border-border bg-card p-4">
                      <div className="shrink-0 mt-0.5">
                        <div className="w-1.5 h-1.5 rounded-full bg-red-500 mt-1.5" />
                      </div>
                      <div className="space-y-1">
                        <p className="text-sm font-semibold text-foreground">{item.name}</p>
                        <p className="text-xs text-muted-foreground leading-relaxed">{item.detail}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </section>

          {/* What GauntletCI checks */}
          <section className="space-y-6 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">What GauntletCI checks: the rule set for breaking change detection</h2>
            <p className="text-muted-foreground leading-relaxed">
              GauntletCI ships a library of deterministic rules that map directly to the
              breaking change categories above. Each rule is applied to the staged diff (the
              lines that are actually changing), so the analysis is scoped to the risk
              introduced by this specific commit, not the entire codebase. The following rules
              are most relevant to breaking change detection:
            </p>
            <div className="space-y-3">
              {[
                {
                  id: "GCI0004",
                  name: "Breaking Change Risk",
                  desc: "Detects removed public methods, properties, and type declarations. Fires when a public signature changes and no backward-compatible overload is provided. Rates every finding high severity.",
                },
                {
                  id: "GCI0003",
                  name: "Behavioral Change Detection",
                  desc: "Identifies diffs where existing logic branches are modified rather than extended. Catches behavioral breaks not reflected in the type system, such as changed default values or inverted conditionals.",
                },
                {
                  id: "GCI0021",
                  name: "Data Schema Compatibility",
                  desc: "Flags changes to database schema definitions, EF Core entity mappings, and migration files that are not paired with a matching migration deployment step.",
                },
                {
                  id: "GCI0038",
                  name: "Dependency Injection Safety",
                  desc: "Detects removal of service registrations and changes to constructor signatures that would cause DI containers to fail at runtime during service resolution.",
                },
                {
                  id: "GCI0016",
                  name: "Concurrency and State Risk",
                  desc: "Flags async void methods outside event handlers, blocking .Result and .Wait() calls in async contexts, static mutable fields, and lock(this) patterns that introduce thread safety breaks.",
                },
                {
                  id: "GCI0047",
                  name: "Naming Contract Alignment",
                  desc: "Detects renames of public members, serialization properties, and API route paths that change the external contract without a corresponding migration or deprecation strategy.",
                },
              ].map((rule) => (
                <div key={rule.id} className="flex gap-4 rounded-lg border border-border bg-card p-4">
                  <div className="shrink-0">
                    <span className="inline-block rounded bg-cyan-950 border border-cyan-800 px-2 py-0.5 text-xs font-mono font-semibold text-cyan-400">{rule.id}</span>
                  </div>
                  <div className="space-y-1">
                    <p className="text-sm font-semibold text-foreground">{rule.name}</p>
                    <p className="text-xs text-muted-foreground leading-relaxed">{rule.desc}</p>
                  </div>
                </div>
              ))}
            </div>
            <p className="text-muted-foreground leading-relaxed">
              Code review alone cannot reliably catch these patterns at the pace modern teams
              merge. Boehm and Basili documented that the cost to fix a defect rises by roughly
              an order of magnitude for each phase it survives past the point of introduction{" "}
              <a href="#cite-4" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[4]</a>
              . The specific multipliers vary by project context, but the directional finding
              has been replicated across multiple software engineering cost models.
              A breaking change caught in the diff before the commit costs a single edit. The
              same breaking change caught in production costs an incident, a rollback, and an
              investigation.{" "}
              <Link href="/why-code-review-misses-bugs" className="text-cyan-400 underline underline-offset-2 hover:text-cyan-300">
                Why code review misses bugs
              </Link>{" "}
              explores this cost curve in more detail.
            </p>
          </section>

          {/* When to detect */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">The right time to detect is before the commit</h2>
            <p className="text-muted-foreground leading-relaxed">
              Finding a breaking change in post-deploy monitoring means a rollback, an incident,
              and a post-mortem. Finding it in code review means a comment and a revision.
              Finding it before the commit is created means a fix before anyone else is
              involved.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Static analysis scoped to the staged diff rather than the entire codebase
              delivers feedback at the only moment when the cost of a fix is minimal: before
              the commit exists. The analysis surface is bounded by the size of the commit, so
              the check completes in milliseconds regardless of codebase size. GauntletCI
              implements this model with deterministic structural rules: every public API
              removal, every serialization contract break, and every unsafe concurrency pattern
              in the diff produces a finding on every run.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Determinism is what separates structural rules from probabilistic analysis. A
              tool that produces inconsistent results trains developers to dismiss findings as
              noise. A structural rule that fires every time a public method is removed without
              a replacement overload carries no false-negative rate for that pattern. Developers
              quickly learn which finding types require mandatory action and which allow
              deliberate acknowledgment, and that distinction preserves the signal value of
              every finding the tool produces.
            </p>
            <div className="grid sm:grid-cols-2 gap-4">
              <div className="rounded-lg border border-border bg-card/50 p-4">
                <p className="text-sm font-semibold text-foreground mb-1">Analyzed in milliseconds</p>
                <p className="text-xs text-muted-foreground leading-relaxed">
                  GauntletCI reads the staged diff, not the entire codebase. Analysis completes
                  in under a second for typical commits, adding no perceptible friction to the
                  development workflow.
                </p>
              </div>
              <div className="rounded-lg border border-border bg-card/50 p-4">
                <p className="text-sm font-semibold text-foreground mb-1">No CI pipeline required</p>
                <p className="text-xs text-muted-foreground leading-relaxed">
                  Runs as a pre-commit hook. The developer sees the risk before the commit is
                  created, not after the PR is opened, the pipeline runs, and a reviewer has
                  already context-switched away.
                </p>
              </div>
              <div className="rounded-lg border border-border bg-card/50 p-4">
                <p className="text-sm font-semibold text-foreground mb-1">Zero false negatives for structural rules</p>
                <p className="text-xs text-muted-foreground leading-relaxed">
                  Rules like GCI0004 and GCI0038 match structural patterns in the diff text.
                  If a public signature is removed, the rule fires. There is no threshold that
                  can be misconfigured to suppress it.
                </p>
              </div>
              <div className="rounded-lg border border-border bg-card/50 p-4">
                <p className="text-sm font-semibold text-foreground mb-1">Works in monorepos and multi-project solutions</p>
                <p className="text-xs text-muted-foreground leading-relaxed">
                  Because the analysis is diff-scoped rather than solution-scoped, GauntletCI
                  works equally well in single-project repositories and large monorepos with
                  dozens of projects sharing public API surfaces.
                </p>
              </div>
            </div>
          </section>

          {/* References */}
          <section className="space-y-4 border-t border-border pt-12">
            <h2 className="text-lg font-bold tracking-tight text-muted-foreground">References</h2>
            <ol className="space-y-3 text-xs text-muted-foreground leading-relaxed list-none pl-0">
              <li id="cite-1" className="flex gap-3">
                <span className="shrink-0 font-semibold text-foreground">[1]</span>
                <span>
                  Dig, D. and Johnson, R. "How Do APIs Evolve? A Story of Refactoring."
                  Journal of Software Maintenance and Evolution, 2006. Studied 147 breaking
                  API changes across open source Java projects and found 80 percent were caused
                  by refactoring.{" "}
                  <a
                    href="https://dl.acm.org/doi/10.1002/smr.328"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-cyan-400 underline underline-offset-2 hover:text-cyan-300"
                  >
                    https://dl.acm.org/doi/10.1002/smr.328
                  </a>
                </span>
              </li>
              <li id="cite-2" className="flex gap-3">
                <span className="shrink-0 font-semibold text-foreground">[2]</span>
                <span>
                  Hora, A., et al. "How Do Developers React When Their Libraries Break?"
                  ICSME 2015.{" "}
                  <a
                    href="https://ieeexplore.ieee.org/document/7332473"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-cyan-400 underline underline-offset-2 hover:text-cyan-300"
                  >
                    https://ieeexplore.ieee.org/document/7332473
                  </a>
                </span>
              </li>
              <li id="cite-3" className="flex gap-3">
                <span className="shrink-0 font-semibold text-foreground">[3]</span>
                <span>
                  Microsoft .NET Breaking Changes documentation.{" "}
                  <a
                    href="https://learn.microsoft.com/en-us/dotnet/core/compatibility/breaking-changes"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-cyan-400 underline underline-offset-2 hover:text-cyan-300"
                  >
                    https://learn.microsoft.com/en-us/dotnet/core/compatibility/breaking-changes
                  </a>
                </span>
              </li>
              <li id="cite-4" className="flex gap-3">
                <span className="shrink-0 font-semibold text-foreground">[4]</span>
                <span>
                  Boehm, B. and Basili, V.R. "Software Defect Reduction Top 10 List."
                  IEEE Computer, 34(1), 2001. Documents the order-of-magnitude cost increase
                  for defects that survive each successive development phase.
                </span>
              </li>
            </ol>
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
              View all detection rules
            </Link>
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}
