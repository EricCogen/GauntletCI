import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Why Code Review Misses Bugs | Code Review Blind Spots",
  description:
    "Code review catches style and obvious logic errors. It routinely misses behavioral drift, contract changes, and implicit assumptions. Here is why.",
  alternates: { canonical: "/why-code-review-misses-bugs" },
  openGraph: { images: [{ url: '/og/why-code-review-misses-bugs.png', width: 1200, height: 630 }] },
  authors: [{ name: "Eric Cogen" }],
};

const blindSpots = [
  {
    title: "Reviewers see what is there, not what was removed",
    body: <>Diffs show additions in green and deletions in red, but human attention naturally gravitates toward the new code being introduced. A removed null guard, a deleted validation step, or a missing error handler is easy to miss in a large diff because the new code path looks complete; it just no longer handles the cases the deleted lines covered. Research by Bacchelli and Bird found that reviewers focus primarily on the correctness of additions and rarely audit deletions with the same rigor.<a href="#cite-2" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[2]</a>{" "}This asymmetry means that behavioral regressions caused by removed defensive code are among the most common review misses in practice, and they appear in postmortems with a note that reads &apos;passed code review.&apos;</>,
    example: "A null check before a repository write is removed during a refactor. The diff shows 40 lines of clean new code. The deletion is on line 8 of a 50-line hunk. Three reviewers approve. The first null input in production throws a NullReferenceException in a code path that was safe for three years.",
  },
  {
    title: "Context switching limits depth",
    body: "A reviewer working through a 400-line diff across 12 files cannot hold the full behavior of every changed function in working memory. Review depth degrades sharply with diff size; studies from Microsoft Research have documented this effect directly, showing that reviewer effectiveness declines as PR size increases beyond roughly 200 lines. The most risky changes are often buried in the middle of a large PR where attention is thinnest and reviewer fatigue is highest. Splitting large PRs helps, but many teams merge large PRs because the cost of splitting them feels higher than the perceived risk of missing something in review.",
    example: "A 600-line PR touches an auth middleware, a data access layer, and three API controllers. The critical change, a removed authorization check in one controller, is on file 9 of 12. It receives 4 seconds of review time and three approvals.",
  },
  {
    title: "Implicit contracts are invisible in the diff",
    body: <>When a method changes its parameter type or a public interface removes a member, the reviewer verifies that all call sites within the PR were updated. But external consumers (serialized payloads in a database, client SDKs, stored procedures, or microservices calling the changed API) are outside the diff entirely. These implicit contracts exist in the runtime, not in the code under review, and no amount of careful reading will surface them. McIntosh et al. found that API contract violations are disproportionately represented in post-merge defect reports relative to their frequency in code, suggesting that reviewers systematically underweight this category of risk.<a href="#cite-3" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[3]</a>{" "}The only way to catch implicit contract breaks is to analyze what the change removed from the public surface, which requires structural analysis of the diff rather than comprehension of the new code.</>,
    example: "A DTO property is renamed from 'userId' to 'user_id' for naming consistency. The PR updates all server-side references. A mobile client serialized against the old field name silently reads null for three weeks before anyone reports an issue.",
  },
  {
    title: "Async and concurrency risk is structurally invisible",
    body: "An async void method, a .Result call on a Task, or a static mutable field accessed without synchronization looks like syntactically normal code to a reviewer who is not specifically scanning for concurrency anti-patterns. These issues require both specialized knowledge and deliberate, focused attention; they cannot be caught by a reader scanning for logical correctness in the usual sense. They rarely appear in checklist-driven reviews because most review checklists target obvious correctness, not threading semantics. In .NET specifically, the deadlock-by-.Result-on-async-method pattern is well documented as a category of bug that code review almost never catches before production exposure, because the code looks identical to correct synchronous code until it is running under real load.",
    example: "A service method is converted to async but one caller is not updated and adds a .Wait() call on the returned Task. In tests the call completes immediately. Under real load the ThreadPool starves and the service deadlocks intermittently with no clear error message.",
  },
  {
    title: "Social pressure compresses review time",
    body: <>PRs that have been waiting in the queue get approved faster as reviewers feel social pressure to unblock colleagues. PRs from senior engineers or tech leads receive less critical review than PRs from junior developers, a pattern observed in qualitative studies of peer review dynamics, including Bacchelli and Bird&apos;s 2013 fieldwork at Microsoft.<a href="#cite-2" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[2]</a>{" "}PRs marked urgent, blocking a release, or associated with a deadline get approved without detailed review. That is precisely when the risk of a shallow review is highest. This dynamic is not a failure of individual discipline; it is a structural property of any human review process under time pressure. The social signal of &apos;approved&apos; is not a reliable proxy for the technical signal of &apos;safe to merge.&apos; They diverge most sharply precisely when rigorous review matters most.</>,
    example: "A critical hotfix PR with five changed files gets three approvals in 11 minutes on a Friday afternoon before a release. One of the changed files removes a rate limiting guard that was preventing a known abuse pattern from reaching the database.",
  },
  {
    title: "Security patterns require specialized, active awareness",
    body: <>Spotting SQL injection risk, identifying a weak cryptographic primitive, recognizing a PII field being written to a log statement, or noticing an authorization gap in a new endpoint requires the reviewer to be actively in a security-focused mental mode for the duration of the review. Most reviewers are not in that mode for every PR, because sustaining that level of vigilance across all review tasks is cognitively expensive. The Boehm and Basili defect cost model shows that security defects found after deployment cost orders of magnitude more to remediate than defects found during development, yet the review process provides no structural mechanism to guarantee that security awareness is applied consistently to every change.<a href="#cite-4" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[4]</a>{" "}Security-specific automated analysis, which applies the same rules every time with no fatigue, is a structural improvement over relying on reviewer attention alone.</>,
    example: "A new logging statement is added for debugging: logger.LogInformation('Processing request for user with token: ' + request.Token). The token is a credential. It ships to production and is indexed by the logging platform, accessible to everyone with log read access.",
  },
  {
    title: "Test coverage gaps are not visible in the diff",
    body: "A reviewer can see that new code was added, but cannot easily determine whether the existing test suite exercises the new or changed code paths in a meaningful way. Coverage tools exist, but they are rarely consulted during review, and they measure line coverage rather than behavioral coverage of the specific delta introduced by the PR. A reviewer approving a change that adds a new error handling branch has no fast way to verify whether a test exercises that branch specifically, whether existing tests happen to hit it incidentally, or whether the behavior is entirely untested. This blind spot compounds the structural gap described in more detail in the companion article on why tests miss bugs; the two mechanisms that were supposed to catch regressions have overlapping blind spots, not complementary ones.",
    example: "A new branch is added to handle HTTP 429 responses from a downstream service. It looks correct on review. No test covers it specifically. The upstream service starts returning 429 three months later and the new handler throws because a variable was not initialized in that code path.",
  },
  {
    title: "Configuration and environment changes escape behavioral review",
    body: "Changes to connection strings, feature flags, environment variable names, default timeout values, or dependency injection lifetimes look innocuous in a diff and are rarely reviewed with the same attention as logic changes. But these changes can alter the runtime behavior of the entire application in ways that no amount of code correctness analysis will reveal. A renamed environment variable silently falls back to a default value. A changed DI lifetime turns a scoped service into a singleton and causes state to leak across requests. These categories of change are structurally invisible to reviewers because their effect only appears at runtime; the code reads correctly, it compiles cleanly, and all tests pass, but the system behaves differently in ways that only manifest under realistic conditions.",
    example: "A service registration is changed from AddScoped to AddSingleton to address a perceived performance concern. The service holds per-request state internally. In tests each request is isolated. In production, state from one user's request bleeds into the next user's request.",
  },
];

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "Why Code Review Misses Bugs",
  "description": "Code review catches style and obvious logic errors. It routinely misses behavioral drift, contract changes, and implicit assumptions. Here is why.",
  "url": "https://gauntletci.com/why-code-review-misses-bugs",
  "author": { "@type": "Person", "name": "Eric Cogen" },
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function WhyCodeReviewMissesBugsPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">The problem</p>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Code review blind spots
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              Code review is not a reliable safety net for behavioral risk. It is excellent
              at catching obvious errors and enforcing style. It is structurally limited at
              catching the changes that cause production incidents.
            </p>
            <div className="flex items-center gap-2 pt-1">
              <span className="text-sm font-medium text-foreground">Eric Cogen</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">Founder, GauntletCI</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <time className="text-sm text-muted-foreground" dateTime="2026-04-20">April 20, 2026</time>
            </div>
          </div>

          {/* What the research says */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">What the research says</h2>
            <p className="text-muted-foreground leading-relaxed">
              The limitations of code review as a defect-finding mechanism are not a matter of
              opinion; they are documented in peer-reviewed research. Czerwonka et al. at
              Microsoft Research conducted a large-scale study of code review outcomes and
              concluded that code reviews are not an effective strategy for finding bugs.<a href="#cite-1" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[1]</a>{" "}
              Most defects found in review are style, naming, and readability issues.
              Functional defects, the kind that cause production failures, escape review
              at a high rate relative to their cost and their impact on users.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Bacchelli and Bird's foundational 2013 study of modern code review documented a
              significant gap between what reviewers intend to accomplish and what they actually
              accomplish. Reviewers report that finding bugs is a primary goal of the review
              process. When outcomes are measured, the dominant finding category is style and
              understanding-related comments, not defects. The expectation that review will
              catch behavioral regressions is not consistently supported by the data from
              teams that have measured it.<a href="#cite-2" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[2]</a>
            </p>
            <div className="rounded-lg border border-amber-500/20 bg-amber-500/5 p-5">
              <p className="text-sm text-amber-400 font-medium leading-relaxed">
                Czerwonka et al. (ICSE 2015): "Code reviews at Microsoft are mostly used
                to improve code and find alternative solutions, not to find bugs."<a href="#cite-1" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[1]</a>{" "}
                The study documented that fewer than 15% of review comments address
                actual functional defects. Subsequent replications report similar
                patterns at different organizations, though rates vary by team size,
                domain, and whether structured checklists are used. Counter-evidence
                exists: teams that enforce mandatory security checklists and strict
                PR size limits report meaningfully higher defect-detection rates
                in review, per Bacchelli and Bird (2013).<a href="#cite-2" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[2]</a>
              </p>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              McIntosh et al. further documented that review coverage, the percentage of
              changes that receive any review at all, correlates with long-term software
              quality outcomes.<a href="#cite-3" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[3]</a>{" "}
              But coverage and rigor are not the same thing. A PR can
              receive a review that is technically thorough on the lines present while
              completely missing a removed guard clause or an implicit contract change.
              The structural blind spots of review exist independent of reviewer effort
              or reviewer skill.
            </p>
          </section>

          {/* Review was designed for a different problem */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">Review was designed for a different problem</h2>
            <p className="text-muted-foreground leading-relaxed">
              Code review was designed for human oversight of intent and readability. It answers
              the question "does this code do what the author intended?" effectively. It answers
              the question "does this change break an assumption made somewhere else in the
              system?" poorly, because the second question requires exhaustive structural
              analysis of the diff, not human pattern recognition under time pressure with
              incomplete knowledge of the full system.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Code review asks a human to read code under time pressure with incomplete
              runtime context. That human brings domain knowledge, intent verification, and
              design judgment that no tool can replicate. The same human cannot reliably
              detect that a removed line was the only guard between a valid state and a
              NullReferenceException surfacing three months later. Detecting that requires
              exhaustive analysis of what was removed. Not comprehension of what remains.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Behavioral drift, contract changes, and removed safety checks are structural
              properties of a diff. Detecting them requires rule-based analysis of what was
              removed, not the holistic comprehension that humans do well. This is the gap
              automated pre-commit analysis closes.
            </p>
          </section>

          {/* Blind spots */}
          <section className="space-y-6">
            <h2 className="text-2xl font-bold tracking-tight">8 categories review consistently misses</h2>
            <figure className="float-right ml-6 mb-4 w-[26%] rounded-lg overflow-hidden border border-border">
              <img
                src="/articles/why-code-review-misses-bugs-hero.png"
                alt="Code diff where a deleted safety check fades into shadow while reviewer attention follows the green additions"
                width={540}
                height={380}
                className="w-full h-auto"
              />
              <figcaption className="px-2 py-2 text-xs text-muted-foreground text-center bg-muted/30 leading-snug">
                Deletions disappear. Additions get scrutinized.
              </figcaption>
            </figure>
            <p className="text-muted-foreground leading-relaxed">
              These are not exotic edge cases. They are the most common root causes of
              production regressions in codebases that have active, well-intentioned code
              review processes. The existence of review does not protect against them in
              any reliable way.
            </p>
            <div className="space-y-4">
              {blindSpots.map((spot, i) => (
                <div key={spot.title} className="rounded-xl border border-border bg-card overflow-hidden">
                  <div className="flex items-start gap-4 p-5">
                    <span className="shrink-0 mt-0.5 text-xs font-mono text-muted-foreground/50 w-5">
                      {String(i + 1).padStart(2, "0")}
                    </span>
                    <div className="space-y-3 min-w-0">
                      <h3 className="font-semibold text-foreground">{spot.title}</h3>
                      <p className="text-sm text-muted-foreground leading-relaxed">{spot.body}</p>
                      <div className="rounded-md bg-background/50 border border-border px-4 py-3">
                        <p className="text-xs font-semibold text-muted-foreground/60 uppercase tracking-widest mb-1">Example</p>
                        <p className="text-xs text-muted-foreground leading-relaxed">{spot.example}</p>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* .NET code example */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">A concrete .NET example</h2>
            <p className="text-muted-foreground leading-relaxed">
              Consider a common refactor in a .NET service layer. A developer is simplifying
              an async method and removes what looks like redundant guard code. The diff looks
              clean and the new code is idiomatic .NET 6+. Every reviewer sees the new code
              as correct. What they will not notice is what was removed and what contracts
              that removal breaks.
            </p>
            <div className="rounded-xl border border-border bg-card overflow-hidden">
              <div className="border-b border-border px-5 py-3 bg-card/80">
                <p className="text-xs font-mono text-muted-foreground/60">UserService.cs -- staged diff</p>
              </div>
              <div className="p-5 font-mono text-xs leading-relaxed space-y-1">
                <p className="text-muted-foreground/50">@@ -38,14 +38,9 @@</p>
                <p className="text-muted-foreground/60">{"  "}public async Task&lt;UserDto&gt; GetUserAsync(int id, CancellationToken ct)</p>
                <p className="text-muted-foreground/60">{"  {"}</p>
                <p className="text-red-400">{"- "}{"    "}if (id &lt;= 0)</p>
                <p className="text-red-400">{"- "}{"        "}throw new ArgumentOutOfRangeException(nameof(id), "Id must be positive.");</p>
                <p className="text-red-400">{"- "}{"    "}if (ct.IsCancellationRequested)</p>
                <p className="text-red-400">{"- "}{"        "}ct.ThrowIfCancellationRequested();</p>
                <p className="text-muted-foreground/60">{"    "}var entity = await _repository.FindByIdAsync(id, ct);</p>
                <p className="text-red-400">{"- "}{"    "}if (entity == null)</p>
                <p className="text-red-400">{"- "}{"        "}throw new NotFoundException("User not found.");</p>
                <p className="text-green-400">{"+ "}{"    "}ArgumentNullException.ThrowIfNull(entity);</p>
                <p className="text-muted-foreground/60">{"    "}return _mapper.Map&lt;UserDto&gt;(entity);</p>
                <p className="text-muted-foreground/60">{"  }"}</p>
              </div>
              <div className="border-t border-border px-5 py-3 bg-red-500/5 space-y-1.5">
                <p className="text-xs text-red-400">
                  GCI0001: Removed guard clause -- ArgumentOutOfRangeException on id &lt;= 0 is no longer thrown. Negative ids now reach the database layer.
                </p>
                <p className="text-xs text-red-400">
                  GCI0014: Exception type changed from NotFoundException to ArgumentNullException. Callers catching NotFoundException will not catch this path.
                </p>
              </div>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              The new code is syntactically correct and idiomatic. ArgumentNullException.ThrowIfNull
              is the recommended .NET 6+ pattern. But two behavioral contracts were broken: negative
              id values now reach the database layer instead of being rejected early, and all callers
              that catch NotFoundException will see an uncaught ArgumentNullException in the new
              path. Neither issue is visible from reading the green lines. Both are immediately
              visible from analyzing what was removed.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This is the class of change that appears in production incident postmortems with
              the note "passed code review." The reviewer was not negligent. The diff structure
              did not make the risk visible. Automated diff analysis is designed specifically
              to close this gap by applying the same structural rules every time with no fatigue
              and no context switching.
            </p>
          </section>

          {/* How PR size amplifies the problem */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">How PR size amplifies every blind spot</h2>
            <p className="text-muted-foreground leading-relaxed">
              Every blind spot described above gets worse as PR size increases. The relationship
              between diff size and review effectiveness is not linear; it degrades sharply
              beyond a threshold. Microsoft Research shows reviewers maintain effective attention
              for roughly 200 to 400 lines of diff. Beyond that, the cognitive load exceeds
              working memory capacity. Reviewers shift from line-level analysis to
              impression-level judgment.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Teams that enforce strict PR size limits see measurably better review outcomes.
              But size limits do not eliminate the structural blind spots; they reduce the
              probability that any specific reviewer misses a specific item. A reviewer
              carefully reading a 100-line diff will not notice that a removed line was the
              only error handler for that code path. Smaller diffs reduce probability.
              They do not change the mechanism.
            </p>
            <div className="grid sm:grid-cols-3 gap-4">
              {[
                {
                  label: "Under 200 lines",
                  detail: "Reviewers maintain reasonable attention. Structural issues still escape, but the probability is lower and reviewers have more bandwidth to notice what was deleted.",
                  color: "border-green-500/20 bg-green-500/5",
                  text: "text-green-400",
                },
                {
                  label: "200 to 400 lines",
                  detail: "Review quality begins to degrade measurably. Reviewers spend more time understanding the overall change and less time auditing individual lines. Deletions are increasingly overlooked.",
                  color: "border-amber-500/20 bg-amber-500/5",
                  text: "text-amber-400",
                },
                {
                  label: "Over 400 lines",
                  detail: "Research documents significant decline in defect detection rate. Reviewers approve based on overall impression. The structural blind spots dominate over any individual finding.",
                  color: "border-red-500/20 bg-red-500/5",
                  text: "text-red-400",
                },
              ].map((item) => (
                <div key={item.label} className={"rounded-lg border " + item.color + " p-4"}>
                  <p className={"text-sm font-semibold " + item.text + " mb-1.5"}>{item.label}</p>
                  <p className="text-xs text-muted-foreground leading-relaxed">{item.detail}</p>
                </div>
              ))}
            </div>
          </section>

          {/* The cost case */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">The cost case for catching issues earlier</h2>
            <p className="text-muted-foreground leading-relaxed">
              Boehm and Basili's foundational work on defect cost across the software lifecycle
              established that defects found after deployment cost substantially more to fix than
              defects found before coding is complete, with multipliers of 10x to 100x
              depending on system type and defect category.<a href="#cite-4" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[4]</a>{" "}
              This ratio is widely cited, but
              the implication for code review is rarely stated explicitly: if review misses a
              defect that was present at commit time, every hour between commit and detection
              multiplies the cost to fix.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              A defect caught by pre-commit analysis costs the developer seconds: read the
              finding, fix the line, re-commit. The same defect caught in code review costs
              the author a context switch, a new commit, a re-review cycle, and potentially
              a re-run of CI. The same defect found in production costs an incident response,
              a root cause analysis, a hotfix branch, an emergency deploy, and postmortem
              documentation: hours to days of engineering time for a finding that could
              have been fixed in under a minute at commit time.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This is not an argument that code review should be removed. It is an argument
              that code review should be the second line of defense, not the first and only
              line. Reviewers focus best when the structural work is already done.
            </p>
            <div className="flex items-stretch gap-2">
              {[
                { label: "Pre-commit", sub: "Seconds to fix", color: "border-green-500/30 bg-green-500/5", text: "text-green-400" },
                { label: "Code review", sub: "Minutes to hours", color: "border-amber-500/30 bg-amber-500/5", text: "text-amber-400" },
                { label: "Post-deploy", sub: "Hours to days", color: "border-red-500/30 bg-red-500/5", text: "text-red-400" },
              ].map((stage) => (
                <div key={stage.label} className={"flex-1 rounded-lg border " + stage.color + " p-4 text-center"}>
                  <p className={"text-sm font-semibold " + stage.text}>{stage.label}</p>
                  <p className="text-xs text-muted-foreground mt-1">{stage.sub}</p>
                </div>
              ))}
            </div>
            <p className="text-xs text-muted-foreground">
              Relative cost of remediating the same defect at different stages. Source: Boehm and Basili, 2001.<a href="#cite-4" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[4]</a>
            </p>
          </section>

          {/* Automation is not a replacement */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Automation is not a replacement for review</h2>
            <p className="text-muted-foreground leading-relaxed">
              Code review provides intent verification, domain knowledge transfer, mentorship,
              and team alignment that no tool can replicate. When a reviewer asks "why did you
              choose this approach instead of the existing pattern?" they are exercising judgment
              that has nothing to do with detecting removed null guards. These two functions
              should not compete; they should be layered.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              What automated diff analysis can do is close the structural blind spots: the
              patterns that require exhaustive analysis of what was removed and changed, not
              human comprehension of what the code does. Running automated checks before the
              PR opens flags behavioral and structural risks that reviewers are likely to miss.
              The goal is not to remove review. The goal is to ensure that by the time a PR
              opens, structural risks are already resolved.
            </p>
            <div className="rounded-lg border border-cyan-500/20 bg-cyan-500/5 p-5">
              <p className="text-sm text-cyan-300 leading-relaxed">
                When reviewers do not have to scan for async anti-patterns, missing null guards,
                removed error handlers, or implicit contract changes, they can spend their full
                attention on what humans do best: verifying intent, catching design problems,
                and sharing context about the system.
              </p>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              Review does not become less valuable. It becomes better spent.
            </p>
          </section>

          {/* Cross-links */}
          <section className="space-y-4 rounded-xl border border-border bg-card/50 p-6">
            <h3 className="font-semibold text-foreground">Related reading</h3>
            <div className="space-y-3">
              <div>
                <Link href="/why-tests-miss-bugs" className="text-sm font-medium text-cyan-400 hover:text-cyan-300 transition-colors">
                  Why tests miss bugs
                </Link>
                <p className="text-xs text-muted-foreground mt-0.5 leading-relaxed">
                  Tests pass but bugs still reach production. The categories of risk that escape
                  test suites and why a green build is not the same as safe code.
                </p>
              </div>
              <div className="border-t border-border pt-3">
                <Link href="/what-is-diff-based-analysis" className="text-sm font-medium text-cyan-400 hover:text-cyan-300 transition-colors">
                  What is diff-based analysis?
                </Link>
                <p className="text-xs text-muted-foreground mt-0.5 leading-relaxed">
                  How analyzing only the changed lines, rather than the whole codebase,
                  produces faster, lower-noise findings that are directly actionable at commit time.
                </p>
              </div>
            </div>
          </section>

          {/* References */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">References</h2>
            <ol className="space-y-4 list-decimal list-inside marker:text-muted-foreground/40">
              <li id="cite-1" className="text-sm text-muted-foreground leading-relaxed pl-2">
                Czerwonka, J., et al. "Code Reviews Do Not Find Bugs: How the Current Code Review
                Best Practice Slows Us Down." ICSE Companion 2015. Microsoft Research.{" "}
                <a
                  href="https://www.microsoft.com/en-us/research/publication/code-reviews-do-not-find-bugs/"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-cyan-400 hover:text-cyan-300 transition-colors break-all"
                >
                  https://www.microsoft.com/en-us/research/publication/code-reviews-do-not-find-bugs/
                </a>
              </li>
              <li id="cite-2" className="text-sm text-muted-foreground leading-relaxed pl-2">
                Bacchelli, A. and Bird, C. "Expectations, Outcomes, and Challenges of Modern Code
                Review." ICSE 2013. ACM Digital Library.{" "}
                <a
                  href="https://dl.acm.org/doi/10.5555/2486788.2486882"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-cyan-400 hover:text-cyan-300 transition-colors break-all"
                >
                  https://dl.acm.org/doi/10.5555/2486788.2486882
                </a>
              </li>
              <li id="cite-3" className="text-sm text-muted-foreground leading-relaxed pl-2">
                McIntosh, S., et al. "The Impact of Code Review Coverage and Code Review
                Participation on Software Quality." MSR 2014. ACM Digital Library.{" "}
                <a
                  href="https://dl.acm.org/doi/10.1145/2597073.2597076"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-cyan-400 hover:text-cyan-300 transition-colors break-all"
                >
                  https://dl.acm.org/doi/10.1145/2597073.2597076
                </a>
              </li>
              <li id="cite-4" className="text-sm text-muted-foreground leading-relaxed pl-2">
                Boehm, B. and Basili, V. R. "Software Defect Reduction Top 10 List."
                IEEE Computer, 34(1), January 2001. Pages 135 to 137.
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
              href="/why-tests-miss-bugs"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              Also: Why tests miss bugs
            </Link>
            <Link
              href="/what-is-diff-based-analysis"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              How diff analysis works
            </Link>
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}
