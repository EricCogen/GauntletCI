import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Real Detections | What GauntletCI Catches",
  description:
    "Annotated real-world examples of bugs GauntletCI detects before they reach code review. Removed guards, async anti-patterns, PII leaks, breaking API changes, and more.",
  alternates: { canonical: "/detections" },
};

const detections = [
  {
    id: "GCI0003",
    title: "Behavioral change: removed null guard",
    severity: "High",
    category: "Behavioral Correctness",
    diff: [
      { type: "context", line: "public async Task<Order> CreateOrderAsync(CreateOrderRequest request)" },
      { type: "context", line: "{" },
      { type: "removed", line: "    if (request is null) throw new ArgumentNullException(nameof(request));" },
      { type: "context", line: "    var order = new Order(request.CustomerId, request.Items);" },
      { type: "context", line: "    return await _repo.SaveAsync(order);" },
      { type: "context", line: "}" },
    ],
    finding: "GCI0003: Guard clause removed at line 3 -- ArgumentNullException no longer thrown on null input. Callers relying on this contract will see NullReferenceException deeper in the call stack.",
  },
  {
    id: "GCI0029",
    title: "PII leak: customer email in structured log",
    severity: "High",
    category: "Security",
    diff: [
      { type: "context", line: "var customer = await _customerService.GetAsync(customerId);" },
      { type: "added", line: '    _logger.LogInformation("Processing order for {Email}", customer.Email);' },
      { type: "context", line: "    await ProcessOrderAsync(customer, order);" },
    ],
    finding: "GCI0029: PII field 'Email' logged at line 2. Structured log sinks (Application Insights, Datadog, Splunk) persist this value. Review data retention and access policies.",
  },
  {
    id: "GCI0016",
    title: "Concurrency: async void event handler",
    severity: "High",
    category: "Concurrency",
    diff: [
      { type: "removed", line: "private async Task OnOrderReceived(object sender, OrderEventArgs e)" },
      { type: "added", line: "private async void OnOrderReceived(object sender, OrderEventArgs e)" },
      { type: "context", line: "{" },
      { type: "context", line: "    await ProcessOrderAsync(e.Order);" },
      { type: "context", line: "}" },
    ],
    finding: "GCI0016: Method changed from async Task to async void at line 1. Exceptions thrown inside async void cannot be caught by the caller and will crash the process in .NET.",
  },
  {
    id: "GCI0004",
    title: "Breaking change: public method signature changed",
    severity: "High",
    category: "API Contracts",
    diff: [
      { type: "removed", line: "public IEnumerable<Product> GetProducts(int categoryId)" },
      { type: "added", line: "public IEnumerable<Product> GetProducts(int categoryId, bool includeArchived)" },
      { type: "context", line: "{" },
      { type: "context", line: "    return _repo.Query(categoryId);" },
      { type: "context", line: "}" },
    ],
    finding: "GCI0004: Required parameter 'includeArchived' added to public method at line 1. Callers in external assemblies compiled against the old signature will throw MissingMethodException at runtime.",
  },
  {
    id: "GCI0012",
    title: "Security: hardcoded connection string",
    severity: "High",
    category: "Security",
    diff: [
      { type: "context", line: "private readonly string _connectionString;" },
      { type: "context", line: "" },
      { type: "removed", line: '    _connectionString = configuration.GetConnectionString("Default");' },
      { type: "added", line: '    _connectionString = "Server=prod-db.internal;Database=orders;User Id=sa;Password=P@ssw0rd!";' },
    ],
    finding: "GCI0010: Hardcoded connection string with embedded credentials at line 4. Credentials committed to version control are compromised. Use IConfiguration or a secrets manager.",
  },
  {
    id: "GCI0007",
    title: "Error handling: exception swallowed silently",
    severity: "Medium",
    category: "Error Handling",
    diff: [
      { type: "context", line: "try" },
      { type: "context", line: "{" },
      { type: "context", line: "    await SendNotificationAsync(order);" },
      { type: "context", line: "}" },
      { type: "removed", line: "catch (Exception ex)" },
      { type: "removed", line: "{" },
      { type: "removed", line: "    _logger.LogError(ex, \"Notification failed for order {OrderId}\", order.Id);" },
      { type: "removed", line: "}" },
      { type: "added", line: "catch { }" },
    ],
    finding: "GCI0007: Exception handler removed or emptied at line 9. Errors in SendNotificationAsync will be silently swallowed. Failures will not surface in logs or monitoring.",
  },
];

const severityColor: Record<string, string> = {
  High: "bg-red-500/10 text-red-400 border border-red-500/20",
  Medium: "bg-amber-500/10 text-amber-400 border border-amber-500/20",
  Low: "bg-blue-500/10 text-blue-400 border border-blue-500/20",
};

const lineColor: Record<string, string> = {
  added: "bg-green-500/10 text-green-300",
  removed: "bg-red-500/10 text-red-300",
  context: "text-muted-foreground/60",
};

const linePrefix: Record<string, string> = {
  added: "+",
  removed: "-",
  context: " ",
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "Real Detections: What GauntletCI Catches",
  "description": "Annotated real-world examples of bugs GauntletCI detects before they reach code review. Removed guards, async anti-patterns, PII leaks, breaking API changes, and more.",
  "url": "https://gauntletci.com/detections",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function DetectionsPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">

        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">Real detections</p>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              What GauntletCI actually catches
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              Six annotated examples from real .NET codebases. These are the patterns that pass
              code review, pass tests, and fail in production. GauntletCI flags them before
              the commit is created.
            </p>
          </div>

          {/* Detections */}
          <div className="space-y-10">
            {detections.map((detection) => (
              <div key={detection.id} className="space-y-3">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-mono text-xs text-muted-foreground/50">{detection.id}</span>
                  <h2 className="text-lg font-semibold text-foreground">{detection.title}</h2>
                  <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${severityColor[detection.severity]}`}>
                    {detection.severity}
                  </span>
                  <span className="text-xs text-muted-foreground/50">{detection.category}</span>
                </div>

                {/* Diff */}
                <div className="rounded-xl border border-border overflow-hidden">
                  <div className="border-b border-border bg-card/60 px-4 py-2 flex items-center gap-2">
                    <div className="flex gap-1.5">
                      <div className="w-2.5 h-2.5 rounded-full bg-red-500/40" />
                      <div className="w-2.5 h-2.5 rounded-full bg-amber-500/40" />
                      <div className="w-2.5 h-2.5 rounded-full bg-green-500/40" />
                    </div>
                    <span className="text-xs font-mono text-muted-foreground/40 ml-1">diff --git</span>
                  </div>
                  <div className="p-4 font-mono text-xs leading-relaxed space-y-0.5 bg-background/50">
                    {detection.diff.map((line, i) => (
                      <div key={i} className={`flex gap-2 px-2 py-0.5 rounded ${lineColor[line.type]}`}>
                        <span className="shrink-0 w-3 select-none">{linePrefix[line.type]}</span>
                        <span className="whitespace-pre">{line.line}</span>
                      </div>
                    ))}
                  </div>
                  <div className="border-t border-border bg-red-500/5 px-4 py-3">
                    <p className="text-xs font-mono text-red-400 leading-relaxed">{detection.finding}</p>
                  </div>
                </div>
              </div>
            ))}
          </div>

          {/* Context */}
          <section className="border-t border-border pt-12 space-y-4">
            <h2 className="text-xl font-bold tracking-tight">These are not theoretical</h2>
            <p className="text-muted-foreground leading-relaxed">
              Every pattern above is based on a real class of production incident common to
              .NET services. GauntletCI's detection rules were built by reverse-engineering
              incident post-mortems to find the structural signatures visible in the diff before
              the change was merged.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The analysis is deterministic. No training data. No probability threshold. The same
              diff produces the same findings every time.
            </p>
          </section>

          {/* CTAs */}
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
