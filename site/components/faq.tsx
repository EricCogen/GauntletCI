const faqs = [
  {
    question: `"I already have Roslyn, SonarQube, and linters. Why do I need another tool screaming at me?"`,
    myth: "GauntletCI is a linter.",
    reality: (
      <>
        Linters care about <em>style</em>. GauntletCI cares about <em>broken assumptions</em>.
        It only speaks up when the exact lines you changed introduce a behavioral risk your existing tests
        did not think to check for.
        <span className="block mt-2 text-sm text-muted-foreground">Result: High signal, near-zero noise.</span>
      </>
    ),
  },
  {
    question: `"If this flags every null check I add, I'm just going to --no-verify it."`,
    myth: `GauntletCI says "This is broken."`,
    reality: (
      <>
        GauntletCI says &quot;This is <strong>unverified</strong>.&quot; It is a prompt for the reviewer to ask:{" "}
        <em>&quot;Did you mean to change this behavior?&quot;</em> We tune for precision over volume.
        If a finding is not relevant, suppress it once in <code>.gauntletci-baseline.json</code> and it never appears again.
      </>
    ),
  },
  {
    question: `"Is this just an LLM wrapper making up fake security risks?"`,
    myth: "AI finds the bugs.",
    reality: (
      <>
        The <strong>detection engine is 100% deterministic Roslyn analysis</strong>. It uses a fixed set of 30+
        rules to identify changes. The AI (which runs 100% offline, locally) is only used to <em>explain</em> the
        finding in plain English so juniors do not have to Google the error code.
      </>
    ),
  },
  {
    question: `"Our CI is already slow. Adding more checks is a non-starter."`,
    myth: "Analysis makes CI slower.",
    reality: (
      <>
        GauntletCI runs <strong>locally in under one second</strong> on just the diff. By catching the risky
        change <em>before</em> you push, you avoid the push, wait 15 minutes, CI fails, fix loop that actually
        kills velocity.
      </>
    ),
  },
  {
    question: `"Our Jira tickets are garbage. If this tool relies on tickets, it's doomed."`,
    myth: "GauntletCI requires perfect tickets.",
    reality: (
      <>
        If the ticket is vague, GauntletCI flags a <strong>Requirement Risk</strong>. This forces a healthy
        conversation: <em>&quot;Hey PM, you said fix the login, but this change touches the payment ledger. Is
        that intentional?&quot;</em> It surfaces scope creep that usually gets rubber-stamped in review.
      </>
    ),
  },
];

export function Faq() {
  return (
    <section id="faq" className="py-24 sm:py-32 bg-muted/30">
      <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8">
        <h2 className="text-3xl font-bold text-center tracking-tight sm:text-4xl mb-16">
          The Honest FAQ for Skeptical Engineers
        </h2>
        <dl className="space-y-6">
          {faqs.map((faq, i) => (
            <div key={i} className="rounded-xl border border-border bg-card p-6">
              <dt className="text-base font-semibold text-foreground">
                {faq.question}
              </dt>
              <dd className="mt-3 text-sm text-muted-foreground leading-relaxed">
                <span className="font-semibold text-foreground">Myth:</span> {faq.myth}
                <br />
                <span className="font-semibold text-foreground">Reality:</span>{" "}
                {faq.reality}
              </dd>
            </div>
          ))}
        </dl>
      </div>
    </section>
  );
}
