import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Why Tests Miss Bugs | The Green Build Fallacy",
  description:
    "Tests pass but bugs still reach production. Learn the 6 categories of structural risk that escape test suites, why code coverage is a misleading proxy, and why a green build is not the same as safe code.",
  alternates: { canonical: "/why-tests-miss-bugs" },
  openGraph: { images: [{ url: '/og/why-tests-miss-bugs.png', width: 1200, height: 630 }] },
};

const categories = [
  {
    title: "Behavioral drift",
    body: "A guard clause, fallback branch, or defensive early-return is quietly removed during a refactor. The developer's intent was to simplify the code, not to change its behavior, but the behavior did change. Existing tests never exercised that removed path because it was never added to the test suite in the first place. Every test still passes. The behavior of the system has silently shifted. These are among the hardest regressions to diagnose in production because the code looks correct: the method is shorter, the logic reads cleanly, and the CI pipeline is green. Static analysis of the diff is the only reliable way to surface this class of change before it ships.",
    example: "A null check before a database write is deleted during a cleanup refactor. No test in the suite covers the null path because all tests pass populated objects. The build is green. On the first null input in production, the database write throws an unhandled exception that corrupts a batch operation and requires a manual data repair.",
  },
  {
    title: "Implicit contract changes",
    body: "A public method's parameter type is widened from int to long, an enum value is renamed or removed, or a method that previously returned null begins throwing instead. The compiler is satisfied if all internal call sites were updated. But external consumers, including other services, serialization layers, stored procedures, mobile clients, and third-party integrations, relied on the old contract shape. That contract was never formally specified or tested from the consumer's perspective, so no test enforces it. The change compiles cleanly and deploys successfully. The implicit breakage surfaces at runtime in a different service, a different tier, or a different team's build.",
    example: "An API response field changes from a JSON string to a nested object during a backend refactor. The serialization layer compiles cleanly. All unit tests mock the response shape and still pass. Consumer services fail at runtime with deserialization exceptions on the first real API call after the deploy, requiring a hotfix and a coordinated rollback.",
  },
  {
    title: "Missing null and edge-case guards",
    body: "A developer adds a new code path that handles the expected happy-path case correctly and thoroughly. The edge cases (null inputs, empty collections, zero values, strings that exceed the expected length, timestamps in the past, and negative numbers in fields that expect positives) are not considered because they were not part of the original requirement or the bug report that prompted the change. Every test written for the new code uses clean, valid inputs and passes. Production surfaces the edge case within days, because real users do not read the assumptions behind the happy path, and real data is rarely as clean as test data.",
    example: "A refactored aggregation method gains a new LINQ operation but the developer forgets to handle an empty source collection. Every test provides a populated list. The first production request that submits an empty list causes an InvalidOperationException inside a LINQ operator, producing a 500 error in a previously stable endpoint and requiring an emergency deploy.",
  },
  {
    title: "Config and environment side effects",
    body: "A change reads a new environment variable, shifts a default timeout from 30 seconds to 5 seconds, introduces a new dependency on a service URL that must be injected, or hardcodes a value that was previously supplied by configuration. Unit tests mock or bypass the environment entirely, so they never touch the configuration surface. Integration tests may exercise the logic path but are run against a test configuration that does not match what production will see. The gap is in the setup, not the logic, and setup failures are invisible to assertion-based test suites because the tests never reach the point where the configuration difference matters.",
    example: "A developer adds a hardcoded database connection string for local development convenience and forgets to remove it before committing. All tests run against the test database using injected configuration and pass. Production ignores the injected environment variable for that connection and routes all traffic through the hardcoded value, writing to the wrong data store until the issue is detected hours later in monitoring.",
  },
  {
    title: "Async and concurrency changes",
    body: "An async void method is introduced where async Task is required, a .Result or .GetAwaiter().GetResult() call blocks a thread pool thread inside an async call chain, or shared mutable state is accessed from multiple concurrent tasks without synchronization. Unit tests run sequentially in a single-threaded environment where race conditions cannot materialize and thread pool exhaustion takes far longer to trigger than in any realistic test duration. The test suite gives no signal. The problem only becomes visible under real concurrency load in production, where it typically manifests intermittently, making it extremely difficult to reproduce, isolate, and diagnose without production observability tooling.",
    example: "A .Result call is introduced inside an async method that runs on the ASP.NET Core synchronization context during a service refactor. Single-threaded unit tests pass in milliseconds without any visible problem. Under production traffic, each concurrent request blocks a thread pool thread while waiting for the inner task, saturating the thread pool progressively and causing request timeouts that escalate into a full application deadlock requiring a service restart.",
  },
  {
    title: "Dependency and schema drift",
    body: "A NuGet package is updated and a previously stable API method changes its signature, adds a new required parameter, or alters its return type in a way the compiler does not catch at all internal call sites. A database migration removes a column that application code still references. A serialization attribute controlling JSON field naming is deleted from a DTO property. Tests are pinned to a specific package version or mock the dependency entirely, so they never encounter the changed interface. The real integration only surfaces when the updated code runs against the real external system, typically on the first deploy to a shared environment or to production.",
    example: "A widely-used NuGet package renames a configuration property in a minor version bump. All unit tests mock the package's interface and pass. The package updates without a compile error, and the build pipeline is green. The first request in production that exercises that configuration path throws a MissingMemberException, taking down the affected endpoint until the configuration is corrected and redeployed.",
  },
];

const dotnetCodeExample = `// ORIGINAL -- GenerateInvoiceAsync before the refactor
public async Task<InvoiceResult> GenerateInvoiceAsync(Order order)
{
    if (order.Items.Count == 0)
        return InvoiceResult.Empty;   // guard: skip empty orders

    var invoice = await _invoiceService.CreateAsync(order);
    await _emailService.SendAsync(order.CustomerEmail, invoice);
    await _auditLog.RecordAsync("invoice_created", order.Id);
    return InvoiceResult.From(invoice);
}

// CHANGED -- guard removed during a "simplification" refactor
public async Task<InvoiceResult> GenerateInvoiceAsync(Order order)
{
    var invoice = await _invoiceService.CreateAsync(order);
    await _emailService.SendAsync(order.CustomerEmail, invoice);
    await _auditLog.RecordAsync("invoice_created", order.Id);
    return InvoiceResult.From(invoice);
}

// THE TEST THAT STILL PASSES after the regression is introduced
[Fact]
public async Task GenerateInvoiceAsync_ValidOrder_CreatesInvoiceAndSendsEmail()
{
    var order = new Order
    {
        Id = Guid.NewGuid(),
        CustomerEmail = "customer@example.com",
        Items = new List<OrderItem> { new OrderItem("Widget", 49.99m) }
    };

    var result = await _sut.GenerateInvoiceAsync(order);

    Assert.Equal(InvoiceResultStatus.Created, result.Status);
    _mockEmailService.Verify(
        x => x.SendAsync(order.CustomerEmail, It.IsAny<Invoice>()),
        Times.Once
    );
}

// WHY IT PASSES: The test covers only the happy path with a populated order.
// The guard that was removed handled empty orders -- a path no existing test covered.
// After the refactor, empty orders now trigger invoice creation, email delivery,
// and audit logging. GauntletCI flags the removed guard clause (GCI0010).`;

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "Why Tests Miss Bugs",
  "description": "Tests pass but bugs still reach production. Learn the 6 categories of structural risk that escape test suites and why a green build is not the same as safe code.",
  "url": "https://gauntletci.com/why-tests-miss-bugs",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function WhyTestsMissBugsPage() {
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
              Why tests miss bugs
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              A green build means your tests passed. It does not mean your code is safe.
              Tests are written to verify what developers expected. They cannot verify
              what developers forgot, or what they removed.
            </p>
            <div className="flex items-center gap-2 pt-1">
              <span className="text-sm font-medium text-foreground">Eric Cogen</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">Founder, GauntletCI</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <time className="text-sm text-muted-foreground" dateTime="2026-04-20">April 20, 2026</time>
            </div>
            <nav className="flex items-center justify-between pt-2 text-sm border-t border-border/50">
              <Link href="/why-code-review-misses-bugs" className="flex items-center gap-1 text-muted-foreground hover:text-cyan-400 transition-colors">
                <span aria-hidden="true">‹</span> Why Code Review Misses Bugs
              </Link>
              <Link href="/what-is-diff-based-analysis" className="flex items-center gap-1 text-muted-foreground hover:text-cyan-400 transition-colors">
                What Is Diff-Based Analysis? <span aria-hidden="true">›</span>
              </Link>
            </nav>
          </div>

          {/* The Green Build Fallacy */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">The Green Build Fallacy</h2>
            <figure className="float-right ml-6 mb-4 w-[300px] rounded-lg overflow-hidden border border-border">
              <img
                src="/articles/why-tests-miss-bugs-hero.png"
                alt="The green build fallacy: CI shows build passed, 23 tests, no warnings — but below: guard removed, no test covered it, leading to production NullReferenceException"
                width={560}
                height={260}
                className="w-full h-auto"
              />
            </figure>
            <p className="text-muted-foreground leading-relaxed">
              Most engineering teams treat a passing CI pipeline as a meaningful safety signal, and it
              is, to a point. A green build confirms that the tests you wrote still pass against the code
              you submitted. What it cannot confirm is that the code behaves correctly under all the
              conditions that matter in production: unexpected inputs, removed guards, changed contracts,
              and environmental differences that no test harness fully replicates.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Test suites are written by human developers at a specific point in time, against a specific
              understanding of the system. They encode what developers expected, not what the system needs
              to do. Every change to the codebase creates new behavioral surface area. Unless someone writes
              a new test at the exact moment of that change, the coverage gap grows silently, change by
              change, deploy by deploy.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The result is a growing divergence between "the tests pass" and "the system behaves correctly
              under all inputs." This gap is where production incidents live. It is not a failure of effort
              or care. It is the inherent structural limit of testing as a verification strategy when tests
              are written against a static snapshot of expectations.
            </p>
            <div className="rounded-lg border border-amber-500/20 bg-amber-500/5 p-5">
              <p className="text-sm text-amber-400 font-medium">
                A 2002 study commissioned by the National Institute of Standards and Technology estimated
                that software defects cost the U.S. economy approximately $59.5 billion annually. The
                report identified inadequate testing infrastructure, not the absence of testing but the
                inability to detect defects introduced during development before they reach production,
                as a primary driver of that cost.{" "}
                <a href="#cite-1" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[1]</a>
                {" "}Counter-evidence note: cost estimates vary by methodology and era; subsequent analyses
                have revised this figure as software complexity has grown.
              </p>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              That cost is not evenly distributed across the development lifecycle. Defects that escape to
              production are consistently far more expensive to fix than defects caught at the source. Boehm
              and Basili found that detecting and correcting a defect in production costs between 10 and 100
              times more than detecting it during development, depending on system type and the phase at
              which it is finally found.{" "}
              <a href="#cite-4" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[4]</a>
              {" "}Counter-evidence note: subsequent research in iterative development environments finds a
              narrower ratio, though the directional finding holds. The categories of bugs that tests miss
              most systematically are also the ones most likely to cause production incidents, because they
              involve changed behavior, not missing behavior, and changed behavior does not show up in tests
              that were written before the change was made.
            </p>
          </section>

          {/* Categories */}
          <section className="space-y-6">
            <h2 className="text-2xl font-bold tracking-tight">6 categories of bugs that escape test suites</h2>
            <p className="text-muted-foreground">
              These are not exotic edge cases. They are the most common root causes behind production
              regressions in .NET codebases, and in every other typed, compiled language ecosystem.
              Each one represents a class of change that developers make routinely, that CI pipelines
              approve without hesitation, and that tests miss because they were written before the change
              existed.
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

          {/* Code Coverage */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">Why code coverage is a misleading proxy for test quality</h2>
            <p className="text-muted-foreground leading-relaxed">
              The most common organizational response to production bugs that escaped testing is to mandate
              higher code coverage thresholds. The intuition is reasonable: if a line was executed by at
              least one test, it was at least exercised. But the measure conflates execution with
              verification. Coverage tells you which lines ran. It says nothing about whether the assertions
              made during that run were correct, complete, or meaningful in any sense that connects to
              production correctness.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              A test that calls a method and makes no assertions will contribute 100 percent line coverage
              to that method. A test that asserts on an incorrect expected value that happens to match the
              current (buggy) behavior will also show as covered and passing. A test written for the old
              behavior of a method will continue to cover that method after the behavior changes, as long
              as the new behavior still satisfies the old assertion. The coverage number stays stable. The
              correctness of the system does not.
            </p>
            <div className="rounded-lg border border-border bg-card/50 p-5 space-y-3">
              <p className="text-sm font-semibold text-cyan-400">Research finding: coverage does not predict fault detection</p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                In a landmark empirical study published at ICSE 2014, Inozemtseva and Holmes analyzed over
                31,000 test suites across multiple open-source Java projects and measured the correlation
                between line coverage, branch coverage, and actual fault detection effectiveness, meaning
                the ability to catch real, previously-discovered bugs. Their conclusion was unambiguous:
                "Coverage is not strongly correlated with test suite effectiveness." The Spearman rank
                correlation between line coverage and fault detection was weak across all studied projects.
                Branch coverage performed modestly better but remained an unreliable predictor of whether
                a test suite would catch real bugs.{" "}
                <a href="#cite-2" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[2]</a>
              </p>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              The Google Testing Blog reached a similar practical conclusion in 2020, noting that code
              coverage is useful as a lower bound: code that is never executed by any test definitely
              cannot be tested by those tests. But it is a poor upper bound. High coverage does not imply
              high confidence. It implies that lines were executed, which is a much weaker guarantee than
              "those lines behave correctly under all conditions that matter."{" "}
              <a href="#cite-5" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[5]</a>
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This matters practically because coverage-driven development creates a false sense of safety
              that is particularly dangerous for the category of bugs tests miss most: removed behavior.
              When a guard clause is deleted from a method, the coverage of that method may actually
              increase: the method now has fewer branches, so the remaining branches are proportionally
              more covered by the existing tests. The coverage metric improves. The system degrades. The
              metric and the safety signal are moving in opposite directions.
            </p>
          </section>

          {/* Mutation Testing */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">The mutation testing gap: what uncaught mutations tell us</h2>
            <p className="text-muted-foreground leading-relaxed">
              Mutation testing offers a more rigorous way to measure test suite quality than line or branch
              coverage. The technique introduces small, deliberate faults into the production codebase (a
              greater-than operator becomes greater-than-or-equal, an addition becomes subtraction, a
              boolean condition is negated, or a return value is changed) and then runs the full test suite
              against each mutated version. If the test suite fails with the mutation present, the mutation
              is "killed." If the tests still pass, the mutation "survived."
            </p>
            <p className="text-muted-foreground leading-relaxed">
              A high mutation survival rate reveals something important and actionable: large portions of
              the codebase can be arbitrarily altered, with exactly the kinds of mistakes that developers
              make in production, without any test noticing. Each survived mutation is a catalog entry of
              real production risk. Every off-by-one mutation that survives corresponds to a class of
              production bug that would also escape the test suite. Every survived negated condition is a
              real inversion bug waiting to be introduced.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Research by Just, Jalali, and Ernst using the Defects4J dataset (ISSTA 2014) found that
              mutation score is a substantially stronger predictor of real fault detection than statement
              coverage alone. Test suites with higher mutation scores, those that successfully kill more
              mutations, were measurably more effective at detecting actual previously-known bugs in the
              studied Java programs. Where line coverage showed weak predictive correlation, mutation score
              showed meaningful predictive correlation with fault detection ability.{" "}
              <a href="#cite-3" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[3]</a>
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The practical obstacle to using mutation testing as a routine safety gate is its computational
              cost. A full mutation testing run on a large .NET codebase using a tool like Stryker.NET can
              take hours, which makes it impractical as a blocking check in a fast-feedback CI pipeline.
              Most teams run it infrequently if at all. The mutation score decays as new code is added
              without tests that cover the new behavioral surface, and the decay is invisible because the
              regular CI pipeline shows no degradation.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              GauntletCI does not run mutation tests at commit time, but its structural rule engine is
              deliberately calibrated to the specific classes of change that mutation testing reveals are
              most commonly uncaught: removed guard clauses, inverted conditions, deleted fallback branches,
              and weakened boundary checks. These are the structural mutations that survive most test suites
              because no test was ever written to assert on the behavior that was removed or inverted.
            </p>
          </section>

          {/* .NET Code Example */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">A .NET example: the test that passes but misses the regression</h2>
            <p className="text-muted-foreground leading-relaxed">
              The following illustrates behavioral drift in a realistic .NET service method. The original
              method has a guard clause that prevents invoice generation and email delivery for orders with
              no line items. During a routine "simplification" refactor, a developer removes the guard to
              reduce nesting and make the method more readable. The existing test suite, covering only
              the happy path with a valid, populated order, passes without modification. The behavioral
              regression ships to production.
            </p>
            <div className="rounded-xl border border-border bg-card overflow-hidden">
              <div className="border-b border-border px-5 py-3">
                <span className="text-xs font-mono text-muted-foreground/60">InvoiceService.cs / InvoiceServiceTests.cs</span>
              </div>
              <pre className="overflow-x-auto p-5 text-xs text-muted-foreground leading-relaxed font-mono whitespace-pre">
                <code>{dotnetCodeExample}</code>
              </pre>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              The test is not a bad test. It correctly verifies that a valid order with items produces a
              created invoice and triggers exactly one email delivery. It was written when the method was
              first implemented and captured the intended behavior at that time. The problem is that it was
              never extended to cover the empty-order path. So when the guard protecting that path was
              removed, nothing in the test suite detected the change.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This is the structural nature of the problem. The test suite was not wrong; it was incomplete
              with respect to the specific change that was made. And that incompleteness is not visible from
              the test results: all tests pass, coverage holds steady or increases, and the CI pipeline
              reports success. The only reliable way to detect this class of regression at commit time is to
              analyze the diff itself, recognizing that a guard clause was removed, and flag it for review
              before the change is pushed. See{" "}
              <Link href="/what-is-diff-based-analysis" className="text-cyan-400 hover:text-cyan-300 underline underline-offset-2">
                what is diff-based analysis
              </Link>{" "}
              for a deeper explanation of why analyzing the change, rather than the test results, is the
              necessary complement to test-based verification.
            </p>
          </section>

          {/* Property-based and fuzz testing */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">Property-based and fuzz testing: why they also miss structural drift</h2>
            <p className="text-muted-foreground leading-relaxed">
              Property-based testing and fuzz testing represent a meaningful step forward from hand-written
              example-based unit tests. A property test generates hundreds or thousands of random inputs and
              verifies that a specified invariant holds across all of them: for example, that sorting a
              list always produces a result of the same length, that a discount calculation always returns a
              value between zero and the order total, or that serializing and then deserializing a record
              produces an identical record. A fuzzer generates millions of structurally unusual or malformed
              inputs to find crashes, assertion failures, or unexpected behavior.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              These techniques are genuinely more powerful than single-example tests within their target
              domain. Property-based testing can discover bugs that no developer would think to test for,
              particularly in parsing, validation, type conversion, and pure computation logic. But they
              share a fundamental structural limitation with all input-driven testing: they can only test
              what they are written to test, and they can only detect failures that are observable through
              the input-output surface they are pointed at.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Structural drift, such as the removal of a guard clause, a changed default timeout value,
              or a deleted defensive fallback, is not detectable by varying inputs. It is detectable
              by analyzing what changed. A property test verifying "CalculateDiscount always returns a value
              between 0 and the order total" will not detect a regression that removes the guard preventing
              discount calculation on empty orders, as long as the new (incorrect) behavior still returns a
              number in that range for the generated inputs. The property holds. The behavior has
              fundamentally changed. The test passes.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Fuzz testing is similarly effective within its domain and similarly blind outside it. It excels
              at finding crashes, memory corruption, parser vulnerabilities, and type confusion. It does not
              detect that a method now sends an email where it previously returned early, or that a timeout
              was silently reduced from 30 seconds to 5 seconds, or that a serialization attribute governing
              JSON field naming was removed from a public DTO. These are behavioral changes caused by deleted
              lines, not new behaviors triggered by unusual inputs.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The practical takeaway is not that property-based or fuzz testing is insufficient; they are
              valuable and worth adopting alongside unit tests. The takeaway is that input-space testing and
              change-space analysis are complementary strategies that cover different classes of risk.
              Input-space testing catches what unusual inputs reveal.{" "}
              <Link href="/what-is-diff-based-analysis" className="text-cyan-400 hover:text-cyan-300 underline underline-offset-2">Diff-based structural analysis</Link>{" "}
              catches what the structure of the change itself reveals. Neither one makes the other redundant.
            </p>
          </section>

          {/* What tests catch well */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">What tests catch well, and what they do not</h2>
            <p className="text-muted-foreground leading-relaxed">
              Understanding where tests are genuinely strong makes it easier to understand where they
              structurally fail. Tests are most effective at catching bugs in isolated, pure logic with
              well-defined and stable input-output contracts: sorting algorithms, parsing functions,
              arithmetic operations, validation rules, state machine transitions. When you can precisely
              specify "given this input, expect this output," and the contract is stable across changes to
              unrelated code, tests provide strong and reliable regression protection. Every future
              regression against that specific behavior will be caught, indefinitely.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Tests become significantly less reliable across several specific structural categories:
            </p>
            <div className="space-y-4">
              {[
                {
                  label: "Integration seams",
                  detail: "When behavior depends on the interaction between two components, such as a service and its database, an HTTP client and a real downstream API, or a method and the precise expectations of all its callers, unit tests that mock the boundary can pass even when the real integration is broken. The mock encodes a specific assumption about how the boundary behaves. If the real contract changes and the mock is not updated to match, the test continues to pass against a fiction. The production integration fails on the first real request.",
                },
                {
                  label: "Temporal and environmental dependencies",
                  detail: "Code that depends on the current time, environment variables, file system state, random number generators, or the availability of external services is hard to test deterministically. Tests that mock these dependencies confirm that the logic path executes correctly given a specific controlled mock value. They do not confirm that the environment interaction itself is correct, or that the behavior is correct across the full range of real values the dependency can produce in a live environment.",
                },
                {
                  label: "Removed behavior",
                  detail: "This is the most systematic and structurally important gap. Tests assert on the presence of behavior: given input X, expect output Y. Standard testing frameworks have no general mechanism to assert on the absence of removed behavior. When a guard clause is deleted, no existing test fails unless that specific guard was explicitly tested in isolation. The deletion is structurally invisible to the test suite. Code coverage may even improve, since the method now has fewer branches and the remaining ones are proportionally more covered.",
                },
                {
                  label: "Cross-cutting side effects",
                  detail: "A change that adds logging, modifies audit trail entries, triggers a background job, emits a metric, or sends a notification is invisible to tests that only assert on the return value of a method. Side effects that were previously not present, or that were previously prevented by a guard clause that was removed, can be introduced or exposed without any test detecting the addition or the unguarding.",
                },
              ].map((item) => (
                <div key={item.label} className="rounded-lg border border-border bg-card/50 p-4 space-y-2">
                  <p className="text-sm font-semibold text-foreground">{item.label}</p>
                  <p className="text-sm text-muted-foreground leading-relaxed">{item.detail}</p>
                </div>
              ))}
            </div>
            <p className="text-muted-foreground leading-relaxed">
              The practical implication is that tests should be understood as a necessary but not sufficient
              condition for production correctness. They are the right tool for verifying intended behavior
              against known inputs. They are not the right tool for detecting structural changes to the
              codebase that alter behavior in ways that were never explicitly specified in any test case.
              For that, you need analysis of the change itself.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This structural gap is parallel to the one in human code review. A reviewer reading a diff for
              correctness will verify that the changed lines look right. They will not necessarily notice
              that a critical line was removed, or that a guard clause protecting a side effect no longer
              appears. See{" "}
              <Link href="/why-code-review-misses-bugs" className="text-cyan-400 hover:text-cyan-300 underline underline-offset-2">
                why code review misses bugs
              </Link>{" "}
              for the parallel analysis of how human review exhibits the same systematic blind spots as
              automated test suites, and why automated structural analysis is needed alongside both.
            </p>
          </section>

          {/* Bridge */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Bridging the gap: diff-based structural analysis</h2>
            <p className="text-muted-foreground leading-relaxed">
              The complementary strategy to test-based verification is structural analysis of the change
              itself. Rather than running code against test inputs, this approach examines what was added
              and, critically, what was removed. Deleted guard clauses, removed null checks, inverted
              conditions, and async antipatterns all produce characteristic diff signatures identifiable
              before a change is committed, when correction costs nothing and developer context is freshest.
              Tests and structural diff analysis cover different risk surfaces, and neither makes the other
              redundant.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              GauntletCI implements this strategy as a pre-commit rule engine. Each rule targets a specific
              class of structural change with a documented production failure mode: removed guard clauses,
              deleted null checks, inverted conditions, async void methods, missing CancellationToken
              propagation, and removed serialization attributes. Each rule exists because that class of
              structural change regularly produces production incidents that a fully green CI pipeline will
              not prevent.
            </p>
            <div className="grid sm:grid-cols-3 gap-4">
              {[
                {
                  label: "Removed logic detection",
                  detail: "Flags removed guard clauses, null checks, fallback branches, and early-return statements that have no corresponding updated test coverage. These are the structural mutations that survive most test suites because no test was written to assert on the behavior that was removed.",
                },
                {
                  label: "API contract analysis",
                  detail: "Detects public method signature changes, removed serialization attributes, renamed enum values, deleted interface members, and parameter type widening that breaks downstream callers at runtime without any compile-time error at internal call sites.",
                },
                {
                  label: "Async and concurrency rules",
                  detail: "Catches async void methods, blocking .Result and .GetAwaiter().GetResult() calls inside async chains, shared state mutations without synchronization, and missing CancellationToken propagation before they cause thread pool exhaustion or deadlocks in production.",
                },
              ].map((item) => (
                <div key={item.label} className="rounded-lg border border-border bg-card/50 p-4">
                  <p className="text-sm font-semibold text-cyan-400 mb-1.5">{item.label}</p>
                  <p className="text-xs text-muted-foreground leading-relaxed">{item.detail}</p>
                </div>
              ))}
            </div>
          </section>

          {/* References */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">References</h2>
            <ol className="space-y-4 list-decimal list-inside">
              {[
                {
                  id: 1,
                  citation: 'NIST. "The Economic Impacts of Inadequate Infrastructure for Software Testing." Planning Report 02-3. National Institute of Standards and Technology, 2002.',
                  url: "https://www.nist.gov/system/files/documents/director/planning/report02-3.pdf",
                },
                {
                  id: 2,
                  citation: 'Inozemtseva, L. and Holmes, R. "Coverage is Not Strongly Correlated with Test Suite Effectiveness." Proceedings of the 36th International Conference on Software Engineering (ICSE). ACM, 2014.',
                  url: "https://dl.acm.org/doi/10.1145/2568225.2568271",
                },
                {
                  id: 3,
                  citation: 'Just, R., Jalali, D., and Ernst, M.D. "Defects4J: A Database of Existing Faults to Enable Controlled Testing Studies for Java Programs." Proceedings of the 2014 International Symposium on Software Testing and Analysis (ISSTA). ACM, 2014.',
                  url: null,
                },
                {
                  id: 4,
                  citation: 'Boehm, B. and Basili, V.R. "Software Defect Reduction Top 10 List." IEEE Computer, vol. 34, no. 1, pp. 135-137, January 2001.',
                  url: null,
                },
                {
                  id: 5,
                  citation: 'Google Testing Blog. "Code Coverage Best Practices." August 2020.',
                  url: "https://testing.googleblog.com/2020/08/code-coverage-best-practices.html",
                },
              ].map((ref) => (
                <li key={ref.id} id={`cite-${ref.id}`} className="text-sm text-muted-foreground leading-relaxed pl-1">
                  {ref.citation}
                  {ref.url && (
                    <>{" "}<a href={ref.url} target="_blank" rel="noopener noreferrer" className="text-cyan-400 hover:text-cyan-300 break-all">{ref.url}</a></>
                  )}
                </li>
              ))}
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
              See the detection rules
            </Link>
            <Link
              href="/why-code-review-misses-bugs"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              Why code review also misses bugs
            </Link>
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}
