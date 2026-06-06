import type { Metadata } from "next";
import { CaseStudyLayout, type CaseStudyDiffLine } from "../_components/case-study-layout";

export const metadata: Metadata = {
  title: "Case Study: Signature Validation Telemetry in IdentityModel | GauntletCI",
  description:
    "A corrected deep dive into AzureAD IdentityModel PR #3410, which added signature-validation telemetry, issuer allowlisting, and validation call-graph changes.",
  alternates: { canonical: "/articles/case-studies/azuread-hardcoded-authority" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const diffLines: CaseStudyDiffLine[] = [
  { type: "context", line: "// New telemetry controls and issuer allowlist" },
  { type: "added", line: "public static bool RecordSignatureValidationTelemetry { get; set; }" },
  { type: "added", line: "public static bool EnableIssuerHostCaching { get; set; }" },
  { type: "added", line: "public static string[] TrackedIssuers" },
  { type: "added", line: "private static readonly ConcurrentDictionary<string, string> _issuerHostCache = new();" },
  { type: "context", line: "" },
  { type: "context", line: "// Issuer cardinality is bounded by allowlisting" },
  { type: "added", line: 'Returns host if in CryptoTelemetry.TrackedIssuers allowlist' },
  { type: "added", line: 'Returns "other" for all non-allowlisted issuers' },
  { type: "context", line: "" },
  { type: "context", line: "// Validation methods were refactored to instance paths to reach telemetry" },
  { type: "removed", line: "static ValidateSignature(...)" },
  { type: "added", line: "ValidateSignature(...) // instance method with _telemetryClient access" },
];

const findingBody = [
  "[GCI0055/GCI0003] Review signal",
  "Signal   : validation method signatures and call paths changed while telemetry was threaded through",
  "Concern  : token validation is security-critical; observability changes must not change validation semantics",
  "",
  "Telemetry cardinality review",
  "Signal   : new issuer/algorithm/key/error dimensions and global tracking knobs",
  "Concern  : ensure allowlisting prevents unbounded cardinality and avoids leaking arbitrary issuer hosts",
].join("\n");

export default function AzureADHardcodedAuthorityPage() {
  return (
    <CaseStudyLayout
      title="Signature Validation Telemetry in IdentityModel"
      description="PR #3410 was not a hardcoded-authority bug. It added telemetry around JWT and SAML signature validation, including issuer allowlisting, key/algorithm dimensions, and refactoring validation methods so they could record telemetry."
      canonicalPath="/articles/case-studies/azuread-hardcoded-authority"
      repo="AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet"
      pr="PR #3410"
      prUrl="https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/pull/3410"
      outcomeLabel="REVIEW"
      outcomeTone="yellow"
      tags={["Telemetry", "Token Validation", "Security"]}
      ruleIds={["GCI0055", "GCI0003"]}
      stats={[
        { value: "29", label: "files changed" },
        { value: "2,250", label: "lines touched" },
        { value: "5", label: "telemetry dimensions" },
      ]}
      sections={[
        {
          title: "What changed",
          children: (
            <>
              <p>
                The PR introduced telemetry tracking for JWT and SAML signature validation. It records
                dimensions such as library version, token algorithm, key algorithm, issuer bucket, and
                validation error. To do that, several validation paths were refactored from static
                helpers toward instance methods that can access a telemetry client.
              </p>
              <p>
                The issuer dimension is intentionally controlled by
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> CryptoTelemetry.TrackedIssuers</code>.
                Hosts not on the allowlist are reported as
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> other</code>
                to avoid unbounded metric-cardinality growth.
              </p>
            </>
          ),
        },
        {
          title: "Why this is risky",
          children: (
            <>
              <p>
                Signature validation is a security-critical path. Even when telemetry is meant to be
                observational, threading a telemetry client through validation changes call shape,
                exception paths, and performance characteristics. Reviewers need to confirm that
                failures still fail closed and that success/failure semantics do not change.
              </p>
              <p>
                Telemetry dimensions also become production contracts. If issuer values are not tightly
                bucketed, a validation endpoint can generate high-cardinality metrics or accidentally
                expose arbitrary tenant/issuer hostnames to downstream telemetry systems.
              </p>
            </>
          ),
        },
        {
          title: "What the original thin page got wrong",
          children: (
            <>
              <p>
                The old case study said the PR introduced a hardcoded
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> login.microsoftonline.com</code>
                authority URL in production validation code. The verified PR does not support that.
                The string appears as an issuer-host example in telemetry documentation, not as a
                production authority override.
              </p>
              <p>
                The accurate case study is still valuable: it shows how observability work in a
                cryptographic validation path should be reviewed for semantic preservation, privacy,
                performance, and cardinality.
              </p>
            </>
          ),
        },
      ]}
      diffTitle="Diff evidence"
      diffFile="src/Microsoft.IdentityModel.Tokens/Telemetry and validation handlers"
      diffLines={diffLines}
      findingTitle="Accurate review signals"
      findingBody={findingBody}
      caveats={[
        "This is not a GCI0010 hardcoded-configuration case; the previous framing was inaccurate.",
        "The telemetry issuer allowlist is an explicit mitigation, so the review question is whether it is wired safely, not whether issuer tracking is inherently wrong.",
        "Benchmarks in the PR body report low overhead, but validation-path changes still deserve focused review because the code is security-critical.",
      ]}
      nextActions={[
        "Verify telemetry remains disabled or bounded by default and cannot emit arbitrary issuer hosts.",
        "Review validation tests for success, signing-key-not-found, unsupported algorithm, and verification-failure paths after the refactor.",
        "Confirm global telemetry knobs and caches are thread-safe and safe for multi-tenant services.",
      ]}
      sources={[
        { label: "PR #3410", href: "https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/pull/3410" },
        { label: "Rule GCI0055", href: "https://gauntletci.com/docs/rules/GCI0055" },
        { label: "Rule GCI0003", href: "https://gauntletci.com/docs/rules/GCI0003" },
      ]}
    />
  );
}
