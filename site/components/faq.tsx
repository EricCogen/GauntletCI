const commonQA = [
  {
    question: "What is diff-based code analysis?",
    answer: (
      <>
        Diff-based analysis looks only at the lines added or removed in a commit or pull request, not the full
        codebase. Every finding GauntletCI produces is directly tied to a change you made, not a pre-existing
        issue in code you never touched.
      </>
    ),
  },
  {
    question: "How is GauntletCI different from traditional static analysis?",
    answer: (
      <>
        Traditional static analysis tools scan the whole codebase for known patterns. GauntletCI scans only
        what changed in the diff and asks whether the change breaks an assumption that your tests may not cover.
        The scope difference is the key: whole-repo vs. exactly what changed.
      </>
    ),
  },
  {
    question: "Why do tests miss bugs?",
    answer: (
      <>
        Tests verify what you <em>expected</em> to happen. They do not verify what you did not expect. A logic
        change that looks safe can alter a guard clause, shift a branch, or orphan a check that tests never
        exercised. GauntletCI flags those behavioral changes in the diff before they reach review.
      </>
    ),
  },
  {
    question: "What is shift-left code analysis?",
    answer: (
      <>
        Shift-left means moving feedback earlier in the development cycle, closer to when code is written.
        GauntletCI gives you change-risk feedback before you commit, which eliminates the push, wait 15 minutes,
        CI fails, fix loop entirely.
      </>
    ),
  },
  {
    question: "How does GauntletCI work with GitHub Actions?",
    answer: (
      <>
        Add the workflow to your repo. It runs on every pull request, diffs the branch against the base,
        posts findings as inline review comments on the exact diff lines that triggered them, and exits with
        code 1 if blocking findings are detected. See the{" "}
        <a href="/docs/integrations" className="text-cyan-400 hover:underline">CI/CD Integrations</a> doc for
        the full YAML.
      </>
    ),
  },
  {
    question: "Is GauntletCI a Roslyn analyzer?",
    answer: (
      <>
        The detection engine is built on Roslyn, but GauntletCI is not a Roslyn analyzer in the traditional
        sense. It does not run during compilation or integrate with the MSBuild diagnostic pipeline. It runs
        as a separate CLI step against a diff, either pre-commit or in CI.
      </>
    ),
  },
  {
    question: "Does GauntletCI support local-first AI?",
    answer: (
      <>
        Yes. The <code>--with-llm</code> flag enriches high-confidence findings with a plain-English
        explanation using a built-in ONNX inference engine running Phi-4 Mini. Run{" "}
        <code>gauntletci model download</code> once to cache the model (~2 GB) locally. No API key,
        no network call at analysis time. The detection itself is always deterministic; the AI only adds context.
      </>
    ),
  },
  {
    question: "Can I use GauntletCI in an air-gapped environment?",
    answer: (
      <>
        Yes. The core tool has no external runtime dependencies. The optional LLM feature uses a
        built-in ONNX engine with a model downloaded once via <code>gauntletci model download</code>.
        After that, no internet access is needed at analysis time. Nothing in GauntletCI phones home.
      </>
    ),
  },
];

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

        <h3 className="text-2xl font-bold text-center tracking-tight mt-20 mb-10">
          Common Questions
        </h3>
        <dl className="space-y-6">
          {commonQA.map((item, i) => (
            <div key={i} className="rounded-xl border border-border bg-card p-6">
              <dt className="text-base font-semibold text-foreground">
                {item.question}
              </dt>
              <dd className="mt-3 text-sm text-muted-foreground leading-relaxed">
                {item.answer}
              </dd>
            </div>
          ))}
        </dl>
      </div>
    </section>
  );
}
