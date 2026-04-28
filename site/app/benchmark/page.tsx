import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "The Silver Benchmark | GauntletCI Detection Accuracy",
  description:
    "618 real .NET OSS pull requests. 30 rules. Precision and recall of each GauntletCI detection rule measured against a labeled corpus of open-source PRs.",
  alternates: { canonical: "/benchmark" },
  openGraph: {
    images: [{ url: "/og/benchmark.png", width: 1200, height: 630 }],
  },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: "The Silver Benchmark",
  description:
    "618 real .NET OSS pull requests. 30 rules. Precision and recall of each GauntletCI detection rule measured against a labeled corpus.",
  url: "https://gauntletci.com/benchmark",
  publisher: {
    "@type": "Organization",
    name: "GauntletCI",
    url: "https://gauntletci.com",
  },
};

type RuleStatus = "passing" | "in-progress" | "limited";

interface ConfusionMatrix {
  tp: number;
  fp: number;
  fn: number;
  tn: number;
}

interface Improvement {
  prevPrecision: string;
  prevRecall: string;
  note: string;
}

interface Rule {
  id: string;
  name: string;
  category: string;
  desc: string;
  matrix: ConfusionMatrix;
  precision: string;
  recall: string;
  f1: string;
  triggerPct: string;
  improvement?: Improvement;
  note?: string;
  notableFP?: string;
  ciP?: string;
  ciR?: string;
  status: RuleStatus;
}

interface NextStep {
  id: string;
  precision: string;
  recall: string;
  gap: string;
}

function metricColor(
  value: string,
  greenThreshold: number,
  amberThreshold: number
): string {
  if (value === "--") return "text-muted-foreground/60";
  const n = parseFloat(value);
  if (isNaN(n)) return "text-muted-foreground/60";
  if (n >= greenThreshold) return "text-green-400";
  if (n >= amberThreshold) return "text-amber-400";
  return "text-red-400";
}

function cardBorderClass(status: RuleStatus): string {
  if (status === "passing") return "border-green-500/15 hover:border-green-500/30";
  if (status === "in-progress") return "border-amber-500/15 hover:border-amber-500/30";
  return "border-border";
}

function RuleCard({ rule }: { rule: Rule }) {
  return (
    <div
      className={`rounded-xl border bg-card/30 p-5 space-y-4 transition-colors ${cardBorderClass(rule.status)}`}
    >
      {/* Header row */}
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 shrink-0">
          {rule.id}
        </span>
        <span className="font-semibold text-foreground">{rule.name}</span>
        <span className="text-xs px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
          {rule.category}
        </span>
        {rule.triggerPct !== "--" ? (
          <span className="ml-auto text-xs text-muted-foreground/50 shrink-0">
            fires on {rule.triggerPct}% of fixtures
          </span>
        ) : (
          <span className="ml-auto text-xs text-muted-foreground/40 shrink-0">
            no trigger data
          </span>
        )}
      </div>

      {/* Description */}
      <p className="text-sm text-muted-foreground leading-relaxed">{rule.desc}</p>

      {/* Stats + Confusion matrix */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        {/* Stat blocks */}
        <div className="space-y-1">
          <p className="text-xs text-muted-foreground/50 mb-2">Metrics</p>
          <div className="grid grid-cols-3 gap-2">
            <div className="text-center p-3 rounded-lg bg-background/50 border border-border/50">
              <div
                className={`text-xl font-bold ${metricColor(rule.precision, 90, 70)}`}
              >
                {rule.precision}
              </div>
              {rule.ciP && rule.ciP !== "--" && (
                <div className="text-xs text-muted-foreground/40 font-mono leading-none mt-0.5">
                  {rule.ciP}
                </div>
              )}
              <div className="text-xs text-muted-foreground/60 mt-0.5">
                Precision
              </div>
            </div>
            <div className="text-center p-3 rounded-lg bg-background/50 border border-border/50">
              <div
                className={`text-xl font-bold ${metricColor(rule.recall, 75, 50)}`}
              >
                {rule.recall}
              </div>
              {rule.ciR && rule.ciR !== "--" && (
                <div className="text-xs text-muted-foreground/40 font-mono leading-none mt-0.5">
                  {rule.ciR}
                </div>
              )}
              <div className="text-xs text-muted-foreground/60 mt-0.5">
                Recall
              </div>
            </div>
            <div className="text-center p-3 rounded-lg bg-background/50 border border-border/50">
              <div
                className={`text-xl font-bold ${metricColor(rule.f1, 82, 60)}`}
              >
                {rule.f1}
              </div>
              <div className="text-xs text-muted-foreground/60 mt-0.5">F1</div>
            </div>
          </div>
        </div>

        {/* Confusion matrix */}
        <div>
          <p className="text-xs text-muted-foreground/50 mb-2">
            Confusion matrix
          </p>
          <div className="grid grid-cols-2 rounded-lg overflow-hidden border border-border text-center text-xs font-mono">
            <div className="bg-green-500/10 text-green-400 p-2 border-b border-r border-border">
              <div className="text-base font-bold">{rule.matrix.tp}</div>
              <div className="text-green-400/50">TP</div>
            </div>
            <div className="bg-red-500/10 text-red-400 p-2 border-b border-border">
              <div className="text-base font-bold">{rule.matrix.fp}</div>
              <div className="text-red-400/50">FP</div>
            </div>
            <div className="bg-amber-500/10 text-amber-400 p-2 border-r border-border">
              <div className="text-base font-bold">{rule.matrix.fn}</div>
              <div className="text-amber-400/50">FN</div>
            </div>
            <div className="bg-muted/20 text-muted-foreground p-2">
              <div className="text-base font-bold">{rule.matrix.tn}</div>
              <div className="text-muted-foreground/50">TN</div>
            </div>
          </div>
        </div>
      </div>

      {/* Improvement row */}
      {rule.improvement && (
        <div className="flex flex-wrap items-center gap-x-5 gap-y-1.5 text-xs py-2.5 px-3 rounded-lg bg-amber-500/5 border border-amber-500/20">
          <span className="text-muted-foreground/60 font-medium shrink-0">
            Improved:
          </span>
          <span>
            <span className="text-muted-foreground/60">P: </span>
            <span className="text-amber-400">
              {rule.improvement.prevPrecision}
            </span>
            <span className="text-muted-foreground/40"> -&gt; </span>
            <span className="text-cyan-400">{rule.precision}</span>
          </span>
          <span>
            <span className="text-muted-foreground/60">R: </span>
            <span className="text-amber-400">
              {rule.improvement.prevRecall}
            </span>
            <span className="text-muted-foreground/40"> -&gt; </span>
            <span className="text-cyan-400">{rule.recall}</span>
          </span>
        </div>
      )}

      {/* Improvement note */}
      {rule.improvement?.note && (
        <p className="text-xs text-muted-foreground/70 leading-relaxed border-l-2 border-cyan-500/30 pl-3">
          {rule.improvement.note}
        </p>
      )}

      {/* Rule note */}
      {rule.note && (
        <p className="text-xs text-muted-foreground/70 leading-relaxed">
          {rule.note}
        </p>
      )}

      {/* Notable FP callout */}
      {rule.notableFP && (
        <div className="rounded-lg bg-background/40 border border-border/60 px-4 py-3 space-y-1">
          <p className="text-xs font-semibold text-muted-foreground/50 uppercase tracking-wide">
            Notable FP
          </p>
          <p className="text-xs font-mono text-muted-foreground/80 leading-relaxed">
            {rule.notableFP}
          </p>
        </div>
      )}
    </div>
  );
}

const passingRules: Rule[] = [
  {
    id: "GCI0003",
    name: "Behavioral Change Detection",
    category: "Correctness",
    desc: "Detects guard clause or null-check removal from non-trivial methods. Callers relying on the contract receive NullReferenceException deeper in the call stack.",
    matrix: { tp: 123, fp: 3, fn: 40, tn: 452 },
    precision: "97.6%",
    recall: "75.5%",
    f1: "85.1%",
    triggerPct: "20.4",
    improvement: {
      prevPrecision: "59.0%",
      prevRecall: "40.4%",
      note: "LogicKeywords narrowed from 7 to 4 tokens; logic-removal threshold raised 5 to 15 lines; empty-catch labeler heuristic redirected to GCI0032.",
    },
    ciP: "±3.0%",
    ciR: "±6.6%",
    status: "passing",
  },
  {
    id: "GCI0004",
    name: "Breaking Change Risk",
    category: "API Contracts",
    desc: "Detects public method signature changes - added required parameters, changed return types, renamed members - that break callers compiled against the previous signature.",
    matrix: { tp: 25, fp: 0, fn: 0, tn: 593 },
    precision: "100%",
    recall: "100%",
    f1: "100%",
    triggerPct: "4.0",
    ciP: "±6.7%",
    ciR: "±6.7%",
    status: "passing",
  },
  {
    id: "GCI0016",
    name: "Async Concurrency Risk",
    category: "Concurrency",
    desc: "Detects async void methods and event handlers. Exceptions thrown inside async void are unobservable and crash the process in .NET.",
    matrix: { tp: 30, fp: 0, fn: 4, tn: 584 },
    precision: "100%",
    recall: "88.2%",
    f1: "93.7%",
    triggerPct: "4.9",
    ciP: "±5.7%",
    ciR: "±11.0%",
    status: "passing",
  },
  {
    id: "GCI0021",
    name: "Data and Schema Compatibility",
    category: "Schema",
    desc: "Detects non-backward-compatible changes to serialization contracts - field removal, type changes, renamed properties without aliases.",
    matrix: { tp: 1, fp: 0, fn: 0, tn: 617 },
    precision: "100%",
    recall: "100%",
    f1: "100%",
    triggerPct: "0.2",
    note: "Low prevalence in corpus (1 fixture). Metrics are directional at this sample size.",
    ciP: "±39.7%",
    ciR: "±39.7%",
    status: "passing",
  },
  {
    id: "GCI0022",
    name: "Idempotency and Retry Safety",
    category: "API Contracts",
    desc: "Detects HTTP POST operations or INSERT statements added without an idempotency key or upsert guard.",
    matrix: { tp: 1, fp: 0, fn: 0, tn: 617 },
    precision: "100%",
    recall: "100%",
    f1: "100%",
    triggerPct: "0.2",
    improvement: {
      prevPrecision: "33.3%",
      prevRecall: "3.0%",
      note: "Labeler was mapping binary/generated file presence to this rule. Replaced with correct idempotency signals: HTTP POST attribute and INSERT without upsert guard.",
    },
    ciP: "±39.7%",
    ciR: "±39.7%",
    status: "passing",
  },
  {
    id: "GCI0024",
    name: "Resource Lifecycle",
    category: "Resources",
    desc: "Detects IDisposable instances created with new without a using statement or explicit Dispose call.",
    matrix: { tp: 25, fp: 2, fn: 5, tn: 586 },
    precision: "92.6%",
    recall: "83.3%",
    f1: "87.7%",
    triggerPct: "4.4",
    improvement: {
      prevPrecision: "38.8%",
      prevRecall: "55.9%",
      note: "Added four skip guards: return new X (caller takes ownership), callee-owns paren check for service registration, static singletons, Enumerator suffix removed from disposable types.",
    },
    ciP: "±10.7%",
    ciR: "±13.1%",
    status: "passing",
  },
  {
    id: "GCI0036",
    name: "Pure Context Mutation",
    category: "Correctness",
    desc: "Detects assignment to a shared field inside a property getter or other pure-context method, breaking the side-effect-free contract.",
    matrix: { tp: 2, fp: 0, fn: 0, tn: 616 },
    precision: "100%",
    recall: "100%",
    f1: "100%",
    triggerPct: "0.3",
    improvement: {
      prevPrecision: "14.3%",
      prevRecall: "33.3%",
      note: "Labeler global early-return replaced with per-file tracking. Generated files (.Designer.cs, .g.cs) excluded. IsNullGuardedInLabelerScope helper added with 20-line lookback.",
    },
    ciP: "±32.9%",
    ciR: "±32.9%",
    status: "passing",
  },
  {
    id: "GCI0039",
    name: "External Service Safety",
    category: "Reliability",
    desc: "Detects HttpClient usage without an explicit timeout or without CancellationToken propagation on outbound HTTP calls.",
    matrix: { tp: 5, fp: 0, fn: 0, tn: 613 },
    precision: "100%",
    recall: "100%",
    f1: "100%",
    triggerPct: "0.8",
    improvement: {
      prevPrecision: "62.5%",
      prevRecall: "55.6%",
      note: "CheckMissingTimeout narrowed from 'HttpClient ' substring to 'new HttpClient(' only - a LoggerMessage attribute string literal was triggering it. IHttpClientFactory factory config guard added. DeleteAsync excluded from CancellationToken check - DynamoDB and AMQP SDKs use the same method name.",
    },
    notableFP:
      "gRPC's GrpcCallInvokerFactory.cs had a [LoggerMessage] attribute with Message = '...only some HttpClient properties...' - the substring 'HttpClient ' triggered the timeout check on a file that never instantiates an HttpClient.",
    ciP: "±21.7%",
    ciR: "±21.7%",
    status: "passing",
  },
  {
    id: "GCI0042",
    name: "TODO/Stub Detection",
    category: "Code Quality",
    desc: "Detects TODO, FIXME, HACK, and NotImplementedException in added lines of non-test production C# files.",
    matrix: { tp: 35, fp: 0, fn: 0, tn: 583 },
    precision: "100%",
    recall: "100%",
    f1: "100%",
    triggerPct: "5.7",
    improvement: {
      prevPrecision: "60.5%",
      prevRecall: "56.1%",
      note: "Rule: for comment lines, marker must be the first token after // - prevents natural-language matches. Labeler: rewritten with per-file rawDiff iteration restricted to non-test .cs files.",
    },
    notableFP:
      "Jellyfin's codec path had '// add a spec-compliant dvh1/dav1 variant before the hvc1 hack variant.' - the word 'hack' used as a codec term in a prose comment, not as a HACK: marker.",
    ciP: "±4.9%",
    ciR: "±4.9%",
    status: "passing",
  },
  {
    id: "GCI0043",
    name: "Nullability and Type Safety",
    category: "Type Safety",
    desc: "Detects the result of an as-cast used without a null check in the same or immediately following expression.",
    matrix: { tp: 66, fp: 2, fn: 17, tn: 533 },
    precision: "97.1%",
    recall: "79.5%",
    f1: "87.4%",
    triggerPct: "11.0",
    improvement: {
      prevPrecision: "73.3%",
      prevRecall: "45.8%",
      note: "Labeler threshold raised from any (1+) null-forgiving operator to count > 1, matching the rule's matchingLines.Count <= 1 early-return guard. This was generating 65 false-negative labels.",
    },
    ciP: "±4.6%",
    ciR: "±8.6%",
    status: "passing",
  },
  {
    id: "GCI0044",
    name: "Performance Hotpath Risk",
    category: "Performance",
    desc: "Detects Thread.Sleep, LINQ enumeration inside loops, and collection .Add() inside unbounded loops.",
    matrix: { tp: 29, fp: 0, fn: 1, tn: 588 },
    precision: "100%",
    recall: "96.7%",
    f1: "98.3%",
    triggerPct: "4.7",
    improvement: {
      prevPrecision: "33.3%",
      prevRecall: "44.0%",
      note: "Labeler rewritten to mirror rule's three-check structure. Unsafe.Add( excluded from .Add() check. Loop detection added for LINQ-in-loop using non-removed-lines context lookback.",
    },
    ciP: "±5.8%",
    ciR: "±8.0%",
    status: "passing",
  },
  {
    id: "GCI0049",
    name: "Float/Double Equality Comparison",
    category: "Type Safety",
    desc: "Detects direct == or != comparisons between float or double values, which fail silently due to IEEE 754 rounding.",
    matrix: { tp: 5, fp: 0, fn: 1, tn: 612 },
    precision: "100%",
    recall: "83.3%",
    f1: "90.9%",
    triggerPct: "0.8",
    ciP: "±21.7%",
    ciR: "±26.7%",
    status: "passing",
  },
];

const inProgressRules: Rule[] = [
  {
    id: "GCI0006",
    name: "Edge Case Handling",
    category: "Correctness",
    desc: "Detects unsafe .Value access on Nullable<T> without a null guard, and public methods adding nullable parameters without validation.",
    matrix: { tp: 86, fp: 4, fn: 62, tn: 466 },
    precision: "95.6%",
    recall: "58.1%",
    f1: "72.3%",
    triggerPct: "14.6",
    improvement: {
      prevPrecision: "43.5%",
      prevRecall: "38.5%",
      note: "Precision fixed: .Value= on LHS skipped, IOptions<T>.Value skipped, constructors excluded, same-line null guard narrowed to regex. Recall gap (62 FNs) remains - the rule processes only added lines and misses .Value access on unchanged context lines.",
    },
    ciP: "±4.6%",
    ciR: "±7.9%",
    status: "in-progress",
  },
  {
    id: "GCI0012",
    name: "Security Risk",
    category: "Security",
    desc: "Detects hardcoded connection strings and credentials embedded directly in source code.",
    matrix: { tp: 2, fp: 2, fn: 0, tn: 614 },
    precision: "50.0%",
    recall: "100%",
    f1: "66.7%",
    triggerPct: "0.6",
    note: "Only 2 positive fixtures in corpus. P=50% reflects 2 FPs out of 4 total fires. Low sample size makes metrics noisy.",
    ciP: "±35.0%",
    ciR: "±32.9%",
    status: "in-progress",
  },
  {
    id: "GCI0032",
    name: "Uncaught Exception Path",
    category: "Error Handling",
    desc: "Detects empty catch blocks, bare catch, and swallowed exceptions where the exception object is never logged or rethrown.",
    matrix: { tp: 55, fp: 1, fn: 81, tn: 481 },
    precision: "98.2%",
    recall: "40.4%",
    f1: "57.2%",
    triggerPct: "9.1",
    note: "Precision is excellent (98.2%). The 81 false negatives are the active work item - the rule's sub-checks cover specific empty-catch patterns but many real exception-swallowing patterns (logging without rethrowing, catch-then-return-null) are not yet detected.",
    ciP: "±4.6%",
    ciR: "±8.1%",
    status: "in-progress",
  },
  {
    id: "GCI0038",
    name: "Dependency Injection Safety",
    category: "Resources",
    desc: "Detects scoped service resolution from root container, missing required service registrations, and lifecycle mismatches in DI configuration.",
    matrix: { tp: 21, fp: 8, fn: 23, tn: 566 },
    precision: "72.4%",
    recall: "47.7%",
    f1: "57.5%",
    triggerPct: "4.7",
    ciP: "±15.5%",
    ciR: "±14.2%",
    status: "in-progress",
  },
  {
    id: "GCI0041",
    name: "Test Quality Gaps",
    category: "Testing",
    desc: "Detects skipped tests, empty assertions, and tests with uninformative names in test project files.",
    matrix: { tp: 11, fp: 4, fn: 0, tn: 603 },
    precision: "73.3%",
    recall: "100%",
    f1: "84.6%",
    triggerPct: "2.4",
    improvement: {
      prevPrecision: "62.5%",
      prevRecall: "80.0%",
      note: "SilencePatterns narrowed: [Skip] split into [Skip] and [Skip( to avoid matching [SkipLocalsInit]. IsTestFile now excludes paths containing 'testdata'. AssertionKeywords expanded with 6 real-world patterns from MongoDB, Azure SDK, ImageSharp, and ASP.NET Core.",
    },
    notableFP:
      "NUnit's testdata directory had [SkipLocalsInit] on a performance-sensitive method. The attribute is a .NET runtime hint with no connection to test skipping - only the substring 'Skip' triggered the match.",
    ciP: "±20.5%",
    ciR: "±12.9%",
    status: "in-progress",
  },
  {
    id: "GCI0045",
    name: "Complexity Control",
    category: "Maintainability",
    desc: "Detects significant increases in cyclomatic complexity, deeply nested control flow, and methods exceeding length thresholds.",
    matrix: { tp: 13, fp: 7, fn: 35, tn: 563 },
    precision: "65.0%",
    recall: "27.1%",
    f1: "38.3%",
    triggerPct: "3.2",
    ciP: "±19.3%",
    ciR: "±12.2%",
    status: "in-progress",
  },
  {
    id: "GCI0046",
    name: "Pattern Consistency Deviation",
    category: "Maintainability",
    desc: "Detects deviations from established patterns in the same codebase - inconsistent error handling, inconsistent async usage, inconsistent null handling.",
    matrix: { tp: 17, fp: 4, fn: 13, tn: 584 },
    precision: "81.0%",
    recall: "56.7%",
    f1: "66.7%",
    triggerPct: "3.4",
    ciP: "±16.2%",
    ciR: "±16.7%",
    status: "in-progress",
  },
];

const limitedRules: Rule[] = [
  {
    id: "GCI0007",
    name: "Error Handling Integrity",
    category: "Error Handling",
    desc: "Detects removal of existing error handling - exception handlers replaced with empty blocks, logging removed from catch clauses.",
    matrix: { tp: 0, fp: 0, fn: 0, tn: 0 },
    precision: "--",
    recall: "--",
    f1: "--",
    triggerPct: "--",
    note: "10 fixtures were added for GCI0007 but labeling was not completed. Metrics are unavailable.",
    status: "limited",
  },
  {
    id: "GCI0010",
    name: "Hardcoding and Configuration",
    category: "Security",
    desc: "Detects hardcoded localhost/private IP URLs and environment-specific configuration embedded in source code.",
    matrix: { tp: 0, fp: 0, fn: 13, tn: 605 },
    precision: "--",
    recall: "0.0%",
    f1: "--",
    triggerPct: "0.0",
    note: "Rule was narrowed from any http:// literal to localhost/private IP only (docs URLs, nuget.org, github.com excluded). The labeler still marks 13 fixtures as positive from the broader original criteria. Rule fires nothing on current corpus.",
    ciP: "--",
    ciR: "±11.4%",
    status: "limited",
  },
  {
    id: "GCI0029",
    name: "PII Entity Logging Leak",
    category: "Security",
    desc: "Detects PII field names (email, ssn, dateofbirth, creditcard, passport, etc.) passed to structured loggers.",
    matrix: { tp: 0, fp: 4, fn: 0, tn: 614 },
    precision: "0.0%",
    recall: "--",
    f1: "--",
    triggerPct: "0.6",
    note: "PII terms were narrowed from 21 to 16 high-confidence terms, removing 'token', 'address', 'username', 'ipaddress', 'deviceid'. The labeler was updated to match. The 4 remaining FPs fire on the corpus but no fixture is labeled positive - the remaining PII term hits are likely false positives the labeler correctly marks as negative.",
    ciP: "±24.5%",
    ciR: "--",
    status: "limited",
  },
  {
    id: "GCI0047",
    name: "Naming/Contract Alignment",
    category: "API Contracts",
    desc: "Detects public member renames that break naming conventions or deviate from established patterns in the same namespace.",
    matrix: { tp: 0, fp: 0, fn: 0, tn: 618 },
    precision: "--",
    recall: "--",
    f1: "--",
    triggerPct: "0.0",
    note: "Rule fires on no fixtures in the corpus. Either the Silver corpus does not contain fixtures where naming contract violations occur, or the rule's detection patterns need adjustment.",
    status: "limited",
  },
];

const nextSteps: NextStep[] = [
  {
    id: "GCI0032",
    precision: "98.2%",
    recall: "40.4%",
    gap: "Recall gap. 81 FNs. Expanding sub-checks for catch-then-return-null, logging without rethrow.",
  },
  {
    id: "GCI0006",
    precision: "95.6%",
    recall: "58.1%",
    gap: "Recall gap. 62 FNs. Rule processes only added lines, missing .Value access on context lines.",
  },
  {
    id: "GCI0045",
    precision: "65.0%",
    recall: "27.1%",
    gap: "Both gaps. The hardest rule to calibrate - cyclomatic complexity signals are noisy by nature.",
  },
  {
    id: "GCI0038",
    precision: "72.4%",
    recall: "47.7%",
    gap: "Both gaps. DI safety patterns are highly framework-specific.",
  },
  {
    id: "GCI0046",
    precision: "81.0%",
    recall: "56.7%",
    gap: "Recall gap. Pattern deviation detection depends on what patterns exist in context lines.",
  },
  {
    id: "GCI0041",
    precision: "73.3%",
    recall: "100%",
    gap: "Precision gap. 4 FPs remain after hardening. Recall is perfect.",
  },
  {
    id: "GCI0012",
    precision: "50.0%",
    recall: "100%",
    gap: "Precision gap. Only 4 total fires, 2 FPs. Insufficient sample to tune further.",
  },
];

export default function BenchmarkPage() {
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-5xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* 1. Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">
              Inside the machine
            </p>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              The Silver Benchmark
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              618 real .NET OSS pull requests. 30 rules. Every number earned
              through iteration.
            </p>
            <p className="text-muted-foreground leading-relaxed max-w-3xl">
              This page documents the precision and recall of each GauntletCI
              detection rule, measured against a labeled corpus of real
              open-source pull requests. The numbers reflect what it took to
              get here: labeler rewrites, rule narrowing, skip-guard additions,
              and calibration passes that surfaced misalignments between what
              a rule detects and what its labeler measured.
            </p>
          </div>

          {/* 2. Summary stat cards */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            {[
              { value: "618", label: "Fixtures" },
              { value: "23", label: "Rules Benchmarked" },
              {
                value: "90.7%",
                label: "Macro Precision",
                sub: "avg across 19 active rules",
              },
              {
                value: "80.9%",
                label: "Macro Recall",
                sub: "avg across 19 active rules",
              },
            ].map((stat) => (
              <div
                key={stat.label}
                className="rounded-xl border border-border bg-card/30 p-5 text-center"
              >
                <div className="text-3xl font-bold text-cyan-400">
                  {stat.value}
                </div>
                <div className="text-sm text-muted-foreground mt-1">
                  {stat.label}
                </div>
                {"sub" in stat && stat.sub && (
                  <div className="text-xs text-muted-foreground/50 mt-0.5">
                    {stat.sub}
                  </div>
                )}
              </div>
            ))}
          </div>

          {/* 3. The Corpus */}
          <section className="border-t border-border pt-12 space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">The Corpus</h2>
            <p className="text-muted-foreground leading-relaxed">
              The Silver corpus contains 618 fixtures drawn from pull requests
              across the most-downloaded .NET open-source projects on GitHub:
              dotnet/aspnetcore, dotnet/runtime, dotnet/efcore,
              StackExchange.Redis, Newtonsoft.Json, NUnit, xUnit, MassTransit,
              gRPC-dotnet, Jellyfin, ImageSharp, and others. Each fixture is a
              raw diff from a real PR, paired with per-rule labels indicating
              whether a finding was expected.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The corpus was built to cover diverse change patterns - refactors,
              feature additions, bug fixes, dependency updates - across
              repositories with very different coding styles. A benchmark built
              from a single project would overfit to one team's conventions. The
              Silver corpus deliberately includes edge cases from projects that
              use patterns unusual enough to trip rules calibrated on more
              conventional codebases.
            </p>
          </section>

          {/* 4. How labeling works */}
          <section className="border-t border-border pt-12 space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">
              How labeling works
            </h2>
            <p className="text-muted-foreground leading-relaxed">
              Silver labels are generated by a heuristic engine
              (SilverLabelEngine) that mirrors each rule's detection logic. They
              are not human-reviewed. Each rule has a dedicated labeler block
              that tracks which files changed, whether they are test files or
              production code, and whether the specific patterns the rule looks
              for appear in the added lines.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              When rule logic is hardened, the labeler is updated to match, and
              all 618 fixtures are re-labeled from scratch. Silver metrics are
              directional - they measure labeler-rule agreement, not ground
              truth correctness. The distinction matters: a rule can achieve
              100% Silver precision and recall while still having real-world
              edge cases the labeler does not model.
            </p>
            <div className="rounded-lg border border-border/50 bg-card/20 px-5 py-4 space-y-2">
              <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground/50">
                On confidence intervals
              </p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                Precision and recall on a small corpus have a known failure
                mode: a rule that fires correctly on 5 fixtures and incorrectly
                on 0 reports 100% precision. That number is accurate on the
                sample but statistically uninformative. A 95% Wilson score
                interval for that result spans 57% to 100%.
              </p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                Per-rule cards include Wilson score confidence intervals
                alongside each reported figure. The Wilson method is preferred
                over the standard Wald interval because it stays well-behaved
                when the proportion is near 0 or 1 and when sample counts are
                small: both conditions apply to several rules here. A wide
                interval is not a criticism of the rule. It is a statement
                about estimation uncertainty given the current corpus size.
              </p>
            </div>
          </section>

          {/* 5. The labeler-rule gap */}
          <section className="border-t border-border pt-12 space-y-6">
            <div className="space-y-3">
              <h2 className="text-2xl font-bold tracking-tight">
                The labeler-rule gap
              </h2>
              <p className="text-muted-foreground leading-relaxed">
                The most common failure mode during calibration was
                labeler-rule misalignment: the labeler and the rule were
                measuring different things, so all metrics were meaningless
                regardless of the numbers. Three examples from the calibration
                log:
              </p>
            </div>

            <div className="space-y-5">
              {/* Example 1: GCI0022 */}
              <div className="rounded-xl border border-border bg-card/20 p-5 space-y-3">
                <div className="flex items-center gap-2">
                  <span className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
                    GCI0022
                  </span>
                  <span className="text-sm font-semibold text-foreground">
                    Complete semantic mismatch
                  </span>
                </div>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  The labeler was mapping binary and generated file presence
                  (.dll, .png, .min.js) to GCI0022 &quot;Idempotency and Retry
                  Safety&quot; - a complete semantic mismatch. The rule looks for HTTP
                  POST operations without idempotency keys. Starting precision:
                  33.3%. Starting recall: 3%. After fixing the labeler to mirror
                  the rule's actual signals, both reached 100%.
                </p>
              </div>

              {/* Example 2: GCI0043 */}
              <div className="rounded-xl border border-border bg-card/20 p-5 space-y-3">
                <div className="flex items-center gap-2">
                  <span className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
                    GCI0043
                  </span>
                  <span className="text-sm font-semibold text-foreground">
                    Early-return guard not reflected in labeler
                  </span>
                </div>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  The labeler emitted a Positive label whenever one or more
                  null-forgiving operators (!.) appeared in added lines. The rule
                  has an early-return guard: it exits without findings when
                  matchingLines.Count &lt;= 1. The labeler was firing on exactly
                  the cases the rule was designed to skip. Result: 65
                  false-negative labels. After raising the labeler threshold to
                  count &gt; 1, recall went from 45.8% to 79.5%.
                </p>
              </div>

              {/* Example 3: GCI0042 */}
              <div className="rounded-xl border border-border bg-card/20 p-5 space-y-3">
                <div className="flex items-center gap-2">
                  <span className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
                    GCI0042
                  </span>
                  <span className="text-sm font-semibold text-foreground">
                    Labeler scope wider than rule scope
                  </span>
                </div>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  The labeler used a global addedLines list spanning all files
                  in the diff - .ps1, .yml, .md, .cs, test files, everything.
                  The rule only processes non-test .cs files. So &quot;# TODO:&quot; in a
                  PowerShell script or &quot;throw new NotImplementedException&quot; in a
                  test file would generate a Positive label that the rule
                  correctly never fired on. After rewriting the labeler to
                  iterate per-file with path-header tracking, recall went from
                  56.1% to 100%.
                </p>
              </div>
            </div>
          </section>

          {/* 6. Per-rule breakdown */}
          <section className="border-t border-border pt-12 space-y-12">
            <h2 className="text-2xl font-bold tracking-tight">
              Per-rule breakdown
            </h2>

            {/* Section A: Passing */}
            <div className="space-y-5">
              <div className="flex flex-wrap items-center gap-3 pb-3 border-b border-green-500/20">
                <div className="w-1.5 h-5 rounded bg-green-500/50 shrink-0" />
                <h3 className="text-lg font-bold text-foreground">Passing</h3>
                <span className="text-sm text-muted-foreground">
                  P &gt;= 90%, R &gt;= 75% - 12 rules
                </span>
              </div>
              <div className="space-y-4">
                {passingRules.map((rule) => (
                  <RuleCard key={rule.id} rule={rule} />
                ))}
              </div>
            </div>

            {/* Section B: In Progress */}
            <div className="space-y-5">
              <div className="flex flex-wrap items-center gap-3 pb-3 border-b border-amber-500/20">
                <div className="w-1.5 h-5 rounded bg-amber-500/50 shrink-0" />
                <h3 className="text-lg font-bold text-foreground">
                  In Progress
                </h3>
                <span className="text-sm text-muted-foreground">
                  Measurable but below threshold - 7 rules
                </span>
              </div>
              <div className="space-y-4">
                {inProgressRules.map((rule) => (
                  <RuleCard key={rule.id} rule={rule} />
                ))}
              </div>
            </div>

            {/* Section C: Limited corpus coverage */}
            <div className="space-y-5">
              <div className="flex flex-wrap items-center gap-3 pb-3 border-b border-border">
                <div className="w-1.5 h-5 rounded bg-muted shrink-0" />
                <h3 className="text-lg font-bold text-foreground">
                  Limited corpus coverage
                </h3>
                <span className="text-sm text-muted-foreground">
                  0 fixtures or no signal - 11 rules
                </span>
              </div>
              <div className="space-y-4">
                {limitedRules.map((rule) => (
                  <RuleCard key={rule.id} rule={rule} />
                ))}
              </div>
            </div>
          </section>

          {/* 7. Precision vs recall */}
          <section className="border-t border-border pt-12 space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">
              Precision vs recall for a pre-commit tool
            </h2>
            <p className="text-muted-foreground leading-relaxed">
              For a pre-commit tool, the cost of false positives and false
              negatives is asymmetric - but not in the way you might expect.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              A false positive (rule fires when it should not) generates noise.
              Developers learn to ignore noisy tools. If a rule fires on every
              commit for non-issues, it gets disabled or bypassed. False
              positive tolerance is near zero for tools that run on every
              commit.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              A false negative (rule does not fire when it should) means a real
              issue reaches code review or production. This is the failure mode
              the tool exists to prevent - but a single missed finding on a
              specific commit is usually less catastrophic than a tool that
              cries wolf on every commit.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The practical implication: during calibration, precision was
              prioritized over recall. A rule at 98% precision / 40% recall
              (GCI0032) is more useful in production than a rule at 70%
              precision / 90% recall. The recall gap is an active improvement
              target; the precision floor is treated as a hard constraint.
            </p>
          </section>

          {/* 8. What comes next */}
          <section className="border-t border-border pt-12 space-y-6">
            <div className="space-y-2">
              <h2 className="text-2xl font-bold tracking-tight">
                What comes next
              </h2>
              <p className="text-muted-foreground">
                Active calibration targets for the 7 in-progress rules:
              </p>
            </div>
            <div className="space-y-3">
              {nextSteps.map((item) => (
                <div
                  key={item.id}
                  className="flex flex-col sm:flex-row sm:items-start gap-2 sm:gap-4 rounded-lg border border-border bg-card/20 px-4 py-3"
                >
                  <span className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 shrink-0 self-start">
                    {item.id}
                  </span>
                  <span className="text-xs font-mono text-amber-400 shrink-0 pt-0.5">
                    {item.precision} / {item.recall}
                  </span>
                  <span className="text-sm text-muted-foreground leading-relaxed">
                    {item.gap}
                  </span>
                </div>
              ))}
            </div>
          </section>

          {/* 9. CTAs */}
          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/docs"
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
            >
              Get started free
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
