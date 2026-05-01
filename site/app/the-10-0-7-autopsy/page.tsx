import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { AuthorBio } from "@/components/author-bio";

export const metadata: Metadata = {
  title: "The 10.0.7 Autopsy: When Working Code is a Security Risk",
  description:
    "A deep dive into the April 2026 .NET 10.0.7 security regression. Why code coverage is a vanity metric and how Behavioral Change Risk can catch critical regressions before production.",
  alternates: { canonical: "/the-10-0-7-autopsy" },
  openGraph: { images: [{ url: '/og/the-10-0-7-autopsy.png', width: 1200, height: 630 }] },
};

const vulnerabilityExample = `// DATAPROTECTION PACKAGE BEFORE 10.0.6 (vulnerable behavior introduced)
public sealed class AuthenticatedEncryptor
{
    private readonly IAuthenticatedEncryptionAlgorithm _algorithm;

    public EncryptionResult Encrypt(byte[] plaintext)
    {
        // OLD BEHAVIOR: HMAC computed over entire ciphertext
        byte[] ciphertext = _algorithm.Encrypt(plaintext);
        byte[] hmacTag = _algorithm.ComputeHmac(ciphertext);
        return new EncryptionResult { Ciphertext = ciphertext, HmacTag = hmacTag };
    }

    public bool TryDecrypt(byte[] ciphertext, byte[] hmacTag)
    {
        // OLD BEHAVIOR: Validate HMAC against ciphertext
        byte[] computedHmac = _algorithm.ComputeHmac(ciphertext);
        return CryptographicEquals(computedHmac, hmacTag);
    }
}

// WHAT THE VULNERABILITY LOOKED LIKE (simplified)
public EncryptionResult Encrypt(byte[] plaintext)
{
    byte[] ciphertext = _algorithm.Encrypt(plaintext);
    // BUG: HMAC now computed over wrong slice of ciphertext
    byte[] hmacTag = _algorithm.ComputeHmac(ciphertext.Skip(16).ToArray());
    return new EncryptionResult { Ciphertext = ciphertext, HmacTag = hmacTag };
}

public bool TryDecrypt(byte[] ciphertext, byte[] hmacTag)
{
    // BUG: But decryption validates the old way (or skips validation entirely in some cases)
    byte[] computedHmac = _algorithm.ComputeHmac(ciphertext);
    // This comparison now fails because encryption and decryption use different slices
    return CryptographicEquals(computedHmac, hmacTag);
}

// UNIT TESTS THAT PASSED despite the regression
[Fact]
public void Encrypt_ValidInput_ReturnsNonEmptyCiphertext()
{
    var encryptor = new AuthenticatedEncryptor();
    var plaintext = new byte[] { 1, 2, 3, 4 };
    
    var result = encryptor.Encrypt(plaintext);
    
    Assert.NotEmpty(result.Ciphertext);
    Assert.NotEmpty(result.HmacTag);
}

[Fact]
public void RoundTrip_PlaintextIsRecovered()
{
    var encryptor = new AuthenticatedEncryptor();
    var plaintext = new byte[] { 1, 2, 3, 4 };
    
    var encrypted = encryptor.Encrypt(plaintext);
    var decrypted = encryptor.Decrypt(encrypted.Ciphertext, encrypted.HmacTag);
    
    Assert.Equal(plaintext, decrypted);
}

// WHAT THE TEST SUITE MISSED
// The regression: encryption and decryption use different slices of the ciphertext.
// The test passes because it encrypts and decrypts in the same process.
// The vulnerability: an attacker forges an HMAC over a different slice than encryption used.
// When a different service decrypts the forged message, the slices match the attacker's expectations,
// and the HMAC validates against forged data.
// The test never covers the cross-service, cross-deployment scenario where encryption and
// decryption happen with different code versions or different module configurations.`;

export default function The1007AutopsyPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">

        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">Security and regression analysis</p>
              <Link href="/articles" className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors">← All articles</Link>
            </div>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              The 10.0.7 Autopsy: When "Working" Code is a Security Risk
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              A regression so subtle it passed thousands of unit tests, yet broke decryption across
              ASP.NET Core deployments worldwide. Why code coverage is a vanity metric when it comes
              to catching trust-boundary violations.
            </p>
            <div className="flex items-center gap-2 pt-1">
              <span className="text-sm font-medium text-foreground">Eric Cogen</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">Founder, GauntletCI</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <time className="text-sm text-muted-foreground" dateTime="2026-04-30">April 30, 2026</time>
            </div>
            <nav className="flex items-center justify-between pt-2 text-sm border-t border-border/50">
              <Link href="/behavioral-change-risk-formal-framework" className="flex items-center gap-1 text-muted-foreground hover:text-cyan-400 transition-colors">
                <span aria-hidden="true">‹</span> Behavioral Change Risk: A Formal Framework
              </Link>
              <Link href="/articles" className="flex items-center gap-1 text-muted-foreground hover:text-cyan-400 transition-colors">
                All articles <span aria-hidden="true">›</span>
              </Link>
            </nav>
          </div>

          {/* The Crisis */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">The April 2026 Crisis</h2>
            <p className="text-muted-foreground leading-relaxed">
              On April 21, 2026, Microsoft released .NET 10.0.7 as an out-of-band security update. This was not a routine package bump. It was an emergency response to a regression in the previous 10.0.6 update that broke decryption for ASP.NET Core applications worldwide.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The fallout was critical. A bug in the <code className="text-sm bg-muted px-1.5 py-0.5 rounded text-muted-foreground">Microsoft.AspNetCore.DataProtection</code> package caused the encryptor to compute its HMAC validation tag over the wrong bytes of the payload. In some cases, the system simply discarded the computed hash entirely. This was a 9.1 severity trust-boundary failure that allowed attackers to forge authentication cookies and gain SYSTEM privileges.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Thousands of ASP.NET Core deployments were affected. The window of vulnerability lasted approximately 18 days: from the release of 10.0.6 (April 3, 2026) through the release of the fix (April 21, 2026). During that window, forged artifacts remained valid even after patch deployment. The implications were severe: API keys issued during the vulnerable period, sessions established through manipulated authentication cookies, and signed tokens that passed validation despite being crafted by an attacker all remained active in production systems, even after teams upgraded to the patched version.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              What made this crisis especially damaging was not just the vulnerability itself, but the detection blind spot. The regression was introduced in a change that modified how many bytes of the ciphertext were included in the HMAC computation. The change looked legitimate in code review: the lines compiled, the syntax was correct, and the logic appeared sound. The unit tests passed. The integration tests passed. Microsoft's own security validation passed. The vulnerability escaped into the wild because the category of risk it represents—behavioral changes to cryptographic boundaries—is invisible to every tool in the standard quality assurance pipeline except one: static analysis of diffs with knowledge of trust boundaries.
            </p>
            <div className="rounded-lg border border-red-500/20 bg-red-500/5 p-5">
              <p className="text-sm text-red-400 font-medium">
                This was not an edge case bug or a rare misconfiguration. This was a cryptographic validation failure in a trusted, widely-used library, discovered only after the damage was already deployable to production.
              </p>
            </div>
          </section>

          {/* The Coverage Myth */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">The Coverage Myth</h2>
            <p className="text-muted-foreground leading-relaxed">
              This vulnerability was not caused by a syntax error. The code was likely valid, compiled perfectly, and potentially passed thousands of unit tests. It is a textbook example of why "code coverage" is a vanity metric.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              You can have 100% line coverage and 100% branch coverage and still ship a critical security hole if your tests do not validate the specific cryptographic intent of the logic branch. A test that confirms "the HMAC computation runs without throwing an exception" is not the same as a test that confirms "the HMAC was computed over exactly these bytes and not those bytes." The first test passes. The second test would have failed.
            </p>

            <figure className="my-8 rounded-xl overflow-hidden border border-border bg-card/50 p-5">
              <pre className="text-xs sm:text-sm overflow-x-auto font-mono text-muted-foreground">
                <code>{vulnerabilityExample}</code>
              </pre>
              <figcaption className="text-xs text-muted-foreground mt-3 italic">
                A simplified example of how the vulnerability could manifest: the encryption routine computes HMAC over a different slice of the ciphertext than the decryption routine expects. All tests pass because they encrypt and decrypt within the same process. The vulnerability emerges across service boundaries.
              </figcaption>
            </figure>

            <p className="text-muted-foreground leading-relaxed">
              The developers at Microsoft who built DataProtection are among the world's most experienced cryptography engineers. The code passed Microsoft's internal review, Microsoft's internal test suite, and months of external use by millions of developers. The bug was not caused by carelessness or a lack of testing rigor. It was caused by a category of risk that testing cannot detect: a silent change to what bytes are being validated, rather than whether validation is happening at all.
            </p>

            <div className="rounded-lg border border-border bg-card/50 p-5 space-y-4">
              <div>
                <p className="text-sm font-semibold text-cyan-400 mb-2">What the tests confirmed</p>
                <ul className="text-sm text-muted-foreground space-y-1">
                  <li>✓ The encryption method accepts a plaintext parameter and returns a ciphertext</li>
                  <li>✓ The decryption method accepts a ciphertext and returns plaintext</li>
                  <li>✓ Round-trip operations (encrypt then decrypt) recover the original message</li>
                  <li>✓ HMAC computation completes without throwing an exception</li>
                  <li>✓ Edge cases like empty input, large input, and null input are handled</li>
                </ul>
              </div>
              <div>
                <p className="text-sm font-semibold text-red-400 mb-2">What the tests could not confirm</p>
                <ul className="text-sm text-muted-foreground space-y-1">
                  <li>✗ The HMAC was computed over the exact bytes that decryption expects to validate</li>
                  <li>✗ The ciphertext slice used in encryption matches the slice used in decryption</li>
                  <li>✗ No architectural change to the trust boundary occurred silently</li>
                  <li>✗ An attacker cannot forge an HMAC by computing it over a different slice</li>
                  <li>✗ Across service boundaries with different code versions, validation remains consistent</li>
                </ul>
              </div>
            </div>

            <p className="text-muted-foreground leading-relaxed">
              This distinction is critical. Tests are written to verify observable behavior: inputs and outputs. They are not written to verify the internals of cryptographic boundaries, and they cannot, because the tests run inside the same process with the same code. A vulnerability that changes what is being hashed, but not whether hashing occurs, is invisible to assertion-based testing.
            </p>
          </section>

          {/* How GauntletCI Should Catch It */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">The Structural Problem: Current Tools Cannot Catch It</h2>
            <p className="text-muted-foreground leading-relaxed">
              Here is the uncomfortable truth: in its current form, even GauntletCI's existing ruleset would not flag this specific vulnerability before merge.
            </p>

            <figure className="my-8 rounded-xl overflow-hidden border border-border bg-card/50 p-5">
              <pre className="text-xs sm:text-sm overflow-x-auto font-mono text-muted-foreground">
                <code>{`// The Diff that should be caught, but isn't
public bool TryDecrypt(byte[] ciphertext, byte[] hmacTag)
{
-   byte[] computedHmac = _algorithm.ComputeHmac(ciphertext);
+   byte[] computedHmac = _algorithm.ComputeHmac(ciphertext.Skip(16).ToArray());
    return CryptographicEquals(computedHmac, hmacTag);
}

public EncryptionResult Encrypt(byte[] plaintext)
{
    byte[] ciphertext = _algorithm.Encrypt(plaintext);
-   byte[] hmacTag = _algorithm.ComputeHmac(ciphertext);
+   byte[] hmacTag = _algorithm.ComputeHmac(ciphertext.Skip(16).ToArray());
    return new EncryptionResult { Ciphertext = ciphertext, HmacTag = hmacTag };
}`}</code>
              </pre>
              <figcaption className="text-xs text-muted-foreground mt-3 italic">
                This diff modifies what bytes are passed to ComputeHmac. The change is small, localized, and symmetric (both encrypt and decrypt change the same way). Current static analysis tools do not have rules that recognize "changed method argument to a cryptographic function" as a behavioral boundary violation.
              </figcaption>
            </figure>

            <p className="text-muted-foreground leading-relaxed">
              The reason is that the pattern is too subtle and context-dependent. GauntletCI's current rules check for:
            </p>

            <ul className="space-y-2 text-sm text-muted-foreground">
              <li className="flex gap-3">
                <span className="text-cyan-400">•</span>
                <span>Logic removal without test changes</span>
              </li>
              <li className="flex gap-3">
                <span className="text-cyan-400">•</span>
                <span>Hardcoded credentials (pattern matching)</span>
              </li>
              <li className="flex gap-3">
                <span className="text-cyan-400">•</span>
                <span>SQL injection (string concatenation with keywords)</span>
              </li>
              <li className="flex gap-3">
                <span className="text-cyan-400">•</span>
                <span>Weak crypto (MD5, SHA1)</span>
              </li>
            </ul>

            <p className="text-muted-foreground leading-relaxed mt-4">
              What they do not check for: "A method that performs validation was called with different arguments than it was called with in the corresponding encryption/encoding path."
            </p>

            <div className="rounded-lg border border-red-500/20 bg-red-500/5 p-5 space-y-3">
              <p className="text-sm text-red-400 font-medium">Why the 10.0.7 vulnerability exists</p>
              <p className="text-sm text-muted-foreground">
                Because there is no rule that catches behavioral changes to cryptographic boundaries where the change is purely in what gets passed to the cryptographic function, not in whether it is called at all. The vulnerability lives in the gap between what testing can verify (does this path execute?) and what static analysis currently checks for (pattern matching and obvious logic removal).
              </p>
            </div>
          </section>

          {/* What Would Catch It */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">What Would Actually Catch It</h2>
            <p className="text-muted-foreground leading-relaxed">
              The vulnerability would require a rule with the following detection logic:
            </p>

            <div className="space-y-4 mt-6">
              <div className="rounded-lg border border-border p-4">
                <h3 className="text-sm font-semibold text-cyan-400 mb-2">Step 1: Identify cryptographic boundaries</h3>
                <p className="text-sm text-muted-foreground">
                  Track all calls to methods known to perform validation or encryption: ComputeHmac, ComputeHash, Encrypt, Decrypt, Sign, Verify, GetHashCode (in crypto contexts).
                </p>
              </div>

              <div className="rounded-lg border border-border p-4">
                <h3 className="text-sm font-semibold text-cyan-400 mb-2">Step 2: Extract argument signatures</h3>
                <p className="text-sm text-muted-foreground">
                  For each method call, record what argument is passed. In our case: ComputeHmac(ciphertext) becomes ComputeHmac(ciphertext.Skip(16).ToArray()). Record both the method name and the argument expression.
                </p>
              </div>

              <div className="rounded-lg border border-border p-4">
                <h3 className="text-sm font-semibold text-cyan-400 mb-2">Step 3: Detect argument changes</h3>
                <p className="text-sm text-muted-foreground">
                  When a diff shows changes to a cryptographic call, extract the "before" and "after" argument. If they differ, this is a behavioral change to a trust boundary.
                </p>
              </div>

              <div className="rounded-lg border border-border p-4">
                <h3 className="text-sm font-semibold text-cyan-400 mb-2">Step 4: Flag and require context</h3>
                <p className="text-sm text-muted-foreground">
                  Generate a finding: "Cryptographic boundary change detected. The argument to ComputeHmac changed from {before} to {after}. Is this intentional? Verify all callers and counterparts use the same argument."
                </p>
              </div>
            </div>

            <p className="text-muted-foreground leading-relaxed mt-6">
              This type of rule requires semantic analysis: understanding that a method is cryptographic in nature, that its arguments matter, and that changes to arguments represent behavioral shifts. It requires more than pattern matching. It requires the tool to understand program structure.
            </p>

            <p className="text-muted-foreground leading-relaxed">
              This is the next frontier for diff-based analysis. GauntletCI has the architecture to support it. The rule simply does not exist yet in the current ruleset. The 10.0.7 crisis is, in part, evidence that this gap exists not just in GauntletCI, but across the entire static analysis ecosystem.
            </p>
          </section>

          {/* Behavioral Change Risk */}
          <section className="space-y-5">
            <p className="text-muted-foreground leading-relaxed">
              The .NET 10.0.7 crisis highlights exactly what I call "Behavioral Change Risk" (BCR): the class of risk that emerges when a code change alters the observable behavior of a system in a way that does not violate compilation, does not violate any existing test assertion, and yet creates new security or functional failure modes in production.
            </p>

            <div className="space-y-4 mt-8">
              <div className="rounded-lg border border-border p-5">
                <h3 className="text-base font-semibold text-foreground mb-2">Invisible Regressions</h3>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  The system "worked," but its underlying behavior regarding trust had shifted. The cryptographic operation still executed. The HTTP response still returned. The unit test still passed. But the trust boundary had moved: the bytes being validated changed. An attacker could now exploit this shift without triggering any alert, test failure, or compile error.
                </p>
              </div>

              <div className="rounded-lg border border-border p-5">
                <h3 className="text-base font-semibold text-foreground mb-2">The Long-Tail Problem</h3>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  Microsoft warns that forged artifacts, like API keys issued during the vulnerable window, remain valid even after you upgrade to 10.0.7. The behavior of your security system changed, and the consequences outlive the patch. Your monitoring will not catch it. Your deployment will not fail. Your users will not report it. But it is there.
                </p>
              </div>

              <div className="rounded-lg border border-border p-5">
                <h3 className="text-base font-semibold text-foreground mb-2">Deterministic Failure Detection</h3>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  We cannot rely on non-deterministic "AI reviews" to catch these shifts. We cannot rely on code coverage metrics. We need rules-first, deterministic analysis to identify when a pull request touches a sensitive logic boundary (like cryptographic validation, authentication, or trust computation) without a corresponding shift in validation. When a developer modifies HMAC computation, there should be a coresponding validation test or validation behavior change. If there is not, the pull request should flag this gap as a behavioral change risk.
                </p>
              </div>
            </div>

            <p className="text-muted-foreground leading-relaxed mt-8">
              We need to stop treating every line in a diff as equal risk. A one-line change to how many bytes are included in an HMAC is not the same as a one-line change to a comment. A change that removes a null check is not the same as a change that reformats a string. It is time for a formal framework that quantifies behavioral risk before the code ever merges.
            </p>
          </section>

          {/* The Validation Gap */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">The Validation Gap</h2>
            <p className="text-muted-foreground leading-relaxed">
              The core insight behind Behavioral Change Risk is the validation gap: the distance between what the code does and what the tests confirm it should do.
            </p>
            <ul className="space-y-3 text-muted-foreground leading-relaxed">
              <li className="flex gap-3">
                <span className="text-cyan-400 font-semibold">Code:</span>
                <span>"Compute an HMAC tag over the ciphertext payload."</span>
              </li>
              <li className="flex gap-3">
                <span className="text-cyan-400 font-semibold">Test:</span>
                <span>"Calling ComputeHmac() does not throw an exception."</span>
              </li>
            </ul>
            <p className="text-muted-foreground leading-relaxed mt-4">
              If you are the reviewer, you see a diff that changes where the HMAC is computed, or over which bytes. If you only have the test result, you see a green build. Both are true, but they describe different aspects of the change. The diff shows the structural change. The test result shows that the code compiles and the happy-path logic still executes. Neither shows that the cryptographic validation boundary has shifted.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Static analysis of the diff can detect this. It cannot guarantee perfect security, but it can raise a flag: "This change modifies how a cryptographic operation is performed. Is that intentional? Have the validation assumptions been reconsidered?" That flag forces deliberate action before the code merges.
            </p>
          </section>

          {/* Conclusion */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">The Path Forward</h2>
            <p className="text-muted-foreground leading-relaxed">
              The 10.0.7 incident is not an indictment of unit testing, integration testing, or code review. It is evidence that these tools are not sufficient alone. They are designed to catch different categories of failure, and behavioral change in security-sensitive code is not one of them.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The path forward requires a new layer of analysis: one that examines the diff itself, understands what changed and why, and raises signals when a behavioral change has no corresponding validation change. This is what Behavioral Change Risk Validation (BCRV) does.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Until this layer is in place, we will continue to see "working code" that is not safe code ship to production, pass all its tests, and wait in the live system for someone to discover the gap.
            </p>
          </section>

          {/* Related Reading */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Related Reading</h2>
            <div className="grid gap-4">
              <Link
                href="/behavioral-change-risk-formal-framework"
                className="group rounded-lg border border-border p-4 hover:border-cyan-400 transition-colors"
              >
                <h3 className="font-semibold group-hover:text-cyan-400 transition-colors">Behavioral Change Risk: A Formal Framework</h3>
                <p className="text-sm text-muted-foreground mt-1">
                  A complete definition of Behavioral Change Risk (BCR) and the Behavioral Change Risk Validation (BCRV) methodology.
                </p>
              </Link>
              <Link
                href="/detect-breaking-changes-before-merge"
                className="group rounded-lg border border-border p-4 hover:border-cyan-400 transition-colors"
              >
                <h3 className="font-semibold group-hover:text-cyan-400 transition-colors">Detect Breaking Changes Before Merge</h3>
                <p className="text-sm text-muted-foreground mt-1">
                  Breaking changes in .NET are often invisible at compile time. Learn the patterns that break callers at runtime.
                </p>
              </Link>
              <Link
                href="/why-tests-miss-bugs"
                className="group rounded-lg border border-border p-4 hover:border-cyan-400 transition-colors"
              >
                <h3 className="font-semibold group-hover:text-cyan-400 transition-colors">Why Tests Miss Bugs</h3>
                <p className="text-sm text-muted-foreground mt-1">
                  Tests pass but bugs still reach production. Learn the categories of structural risk that escape test suites.
                </p>
              </Link>
            </div>
          </section>

        </div>

        <Footer />
      </main>
    </>
  );
}
