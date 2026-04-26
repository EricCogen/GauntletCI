export function BehavioralChangeRisk() {
  return (
    <section id="behavioral-change-risk" className="py-20 sm:py-24 bg-muted/20 border-y border-border">
      <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-12">
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">Definition</p>
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            What is Behavioral Change Risk?
          </h2>
        </div>
        <div className="space-y-5 text-muted-foreground leading-relaxed text-base max-w-3xl mx-auto">
          <p>
            Behavioral Change Risk is the risk that a code change alters runtime behavior in a way
            that is not clearly intentional, reviewed, or validated.
          </p>
          <p>
            It appears when a diff changes a contract, branch, guard clause, exception path, data
            shape, async flow, or side effect without matching validation.
          </p>
          <p>
            GauntletCI treats those changes as review-critical because they are exactly the kinds of
            regressions that pass tests and look harmless in pull requests.
          </p>
        </div>
        <div className="mt-10 mx-auto max-w-2xl rounded-xl border border-cyan-500/30 bg-cyan-500/5 px-8 py-5 text-center">
          <p className="font-mono text-sm text-cyan-400 tracking-wide">
            Behavior changed + validation missing = Behavioral Change Risk
          </p>
        </div>
      </div>
    </section>
  );
}
