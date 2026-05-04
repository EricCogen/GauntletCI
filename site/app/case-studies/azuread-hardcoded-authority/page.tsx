import type { Metadata } from "next";
import Link from "next/link";
import JsonLd from "@/components/json-ld";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Case Study: Hardcoded Authority URL in Azure AD Identity Model | GauntletCI",
  description:
    "GauntletCI catches a hardcoded authority URL in AzureAD identity model PR#3410. Breaks sovereign cloud deployments and prevents environment-specific configuration. Rule GCI0010.",
  alternates: { canonical: "/case-studies/azuread-hardcoded-authority" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: "Case Study: Hardcoded Authority URL in Azure AD Identity Model",
  description:
    "GauntletCI catches a hardcoded authority URL in AzureAD identity model PR#3410. Breaks sovereign cloud deployments and prevents environment-specific configuration. Rule GCI0010.",
  url: "https://gauntletci.com/case-studies/azuread-hardcoded-authority",
  author: { "@type": "Organization", name: "GauntletCI" },
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
  datePublished: "2025-05-03"
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

const diff: { type: string; line: string }[] = [
  { type: "context", line: "// Added in production identity model code:" },
  { type: "added", line: "private string ValidateSignature(string token)" },
  { type: "added", line: "{" },
  { type: "added", line: '    var authority = "https://login.microsoftonline.com/";  // GCI0010: hardcoded' },
  { type: "added", line: "    // ... validation logic using hardcoded authority" },
  { type: "added", line: "}" },
];

const finding = [
  "[GCI0010] Hardcoding and Configuration",
  "Location : src/.../ValidateSignature method",
  "Summary  : Possible hardcoded configuration value ('authority' assigned a string literal).",
  'Evidence : var authority = "https://login.microsoftonline.com/";',
  "Why      : Hardcoded URLs and configuration values prevent environment-specific",
  "           overrides and will silently misbehave in staging, sovereign cloud, or",
  "           air-gapped deployments.",
  "Action   : Inject via constructor, read from configuration, or use a constant",
  "           defined in a dedicated configuration class.",
].join("\n");

export default function AzureADHardcodedAuthorityPage() {
  return (
    <>
      <JsonLd data={jsonLd} />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">
                Case Study
              </p>
              <Link
                href="/case-studies"
                className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors"
              >
                ← All case studies
              </Link>
            </div>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Hardcoded Authority URL in Azure AD Identity Model
            </h1>
            <div className="flex flex-wrap items-center gap-3">
              <span className="font-mono text-sm text-muted-foreground">
                AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet
              </span>
              <a
                href="https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/pull/3410"
                target="_blank"
                rel="noopener noreferrer"
                className="font-mono text-sm text-cyan-400 hover:text-cyan-300 transition-colors"
              >
                PR#3410 ↗
              </a>
            </div>
            <div className="flex flex-wrap gap-2">
              <Link
                href="/docs/rules/GCI0010"
                className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 hover:bg-cyan-500/20 transition-colors"
              >
                GCI0010
              </Link>
              <Link
                href="/docs/rules/GCI0003"
                className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 hover:bg-cyan-500/20 transition-colors"
              >
                GCI0003
              </Link>
              <span className="text-xs font-semibold text-red-400 bg-red-500/10 border border-red-500/20 px-2 py-0.5 rounded-full">
                BLOCK
              </span>
              <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
                Configuration
              </span>
              <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
                Security
              </span>
            </div>
          </div>

          {/* Context */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Context</h2>
            <p className="text-muted-foreground leading-relaxed">
              Azure Active Directory identity model extensions PR#3410 introduced a
              hardcoded authority URL string in production code. Authority URLs in
              identity libraries must be configurable - they vary by Azure
              environment (global, US Government, China, Germany), by tenant, and
              by deployment stage. Hardcoding one means sovereign cloud deployments
              silently authenticate against the wrong authority, and local/staging
              environments cannot override it without patching the binary.
            </p>
          </section>

          {/* Diff */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Diff evidence</h2>
            <div className="rounded-xl border border-border overflow-hidden">
              <div className="border-b border-border bg-card/60 px-4 py-2 flex items-center gap-2">
                <div className="flex gap-1.5">
                  <div className="w-2.5 h-2.5 rounded-full bg-red-500/40" />
                  <div className="w-2.5 h-2.5 rounded-full bg-amber-500/40" />
                  <div className="w-2.5 h-2.5 rounded-full bg-green-500/40" />
                </div>
                <span className="text-xs font-mono text-muted-foreground/40 ml-1">
                  src/Microsoft.IdentityModel.Tokens/Validators.cs
                </span>
              </div>
              <div className="p-4 font-mono text-xs leading-relaxed space-y-0.5 bg-background/50">
                {diff.map((line, i) => (
                  <div
                    key={i}
                    className={`flex gap-2 px-2 py-0.5 rounded ${lineColor[line.type]}`}
                  >
                    <span className="shrink-0 w-3 select-none">
                      {linePrefix[line.type]}
                    </span>
                    <span className="whitespace-pre">{line.line}</span>
                  </div>
                ))}
              </div>
              <div className="border-t border-border bg-red-500/5 px-4 py-3">
                <pre className="text-xs font-mono text-red-400 leading-relaxed whitespace-pre-wrap">
                  {finding}
                </pre>
              </div>
            </div>
          </section>

          {/* Why it matters */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Why it matters</h2>
            <p className="text-muted-foreground leading-relaxed">
              Azure Active Directory is used by hundreds of thousands of enterprise
              applications for authentication. The identity model extensions library
              is the foundation of all MSAL and ADAL token validation. A hardcoded
              authority URL means sovereign cloud customers (US Government, Azure
              China, Azure Germany) who use different login endpoints cannot
              configure the correct authority without forking the library. Staging
              environments that point at a test tenant also cannot override it. The
              failure mode is silent - tokens validate against the wrong authority,
              potentially accepting tokens from unintended tenants or rejecting
              legitimate ones.
            </p>
          </section>

          {/* Rule links */}
          <section className="border-t border-border pt-10 space-y-4">
            <h2 className="text-lg font-semibold">Detection rules</h2>
            <ul className="space-y-2 text-sm text-muted-foreground">
              <li>
                <Link
                  href="/docs/rules/GCI0010"
                  className="text-cyan-400 hover:text-cyan-300 transition-colors"
                >
                  GCI0010 - Hardcoding and Configuration
                </Link>{" "}
                - flags string literals assigned to variables named after
                configuration concepts (authority, url, endpoint, host, etc.).
              </li>
              <li>
                <Link
                  href="/docs/rules/GCI0003"
                  className="text-cyan-400 hover:text-cyan-300 transition-colors"
                >
                  GCI0003 - Behavioral Change Detection
                </Link>{" "}
                - flags changes that alter externally observable behavior including
                authentication flows.
              </li>
            </ul>
          </section>

          {/* Navigation */}
          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/case-studies"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              ← All case studies
            </Link>
            <Link
              href="/docs"
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
            >
              Get started free
            </Link>
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}
