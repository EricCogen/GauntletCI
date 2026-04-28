export function WhyExists() {
  return (
    <section className="py-20 sm:py-28">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-3xl">
          <div className="text-center mb-10">
            <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">
              Why this exists
            </p>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
              A single line caused a production incident
            </h2>
          </div>

          <div className="space-y-6 text-muted-foreground leading-relaxed">
            <p>
              The change was small. One line removed from a service that had been stable for two years.
              The null check was flagged in review as redundant. Tests passed. The reviewer approved.
              It shipped on a Friday.
            </p>
            <p>
              By Monday, support tickets were coming in. The null reference was not redundant - callers
              in three other services relied on the early exception to distinguish between missing input
              and a downstream failure. Without it, errors surfaced two hops away with no context.
              The bug took four hours to trace back to that one removed line.
            </p>
            <p>
              What made it painful was not the incident itself. It was that the risk was visible in the
              diff the whole time. The check was removed. The callers were not updated. No test covered
              the new behavior. Every tool in the pipeline had seen the change and said nothing.
            </p>

            <div className="rounded-xl border border-border bg-muted/40 px-6 py-5 my-8">
              <p className="text-foreground font-medium leading-relaxed">
                "Tests tell you if the code works. Code review tells you if the change looks right.
                Neither tells you if the change is safe."
              </p>
            </div>

            <p>
              GauntletCI was built to close that gap. Not to replace tests or review - both still matter -
              but to add a layer that answers the question neither one asks: did this change introduce
              behavior that is not validated anywhere?
            </p>
            <p>
              The tool runs locally, analyzes only the diff, and surfaces up to three findings before
              the commit is created. The feedback loop is as tight as possible: you see the risk before
              it reaches anyone else.
            </p>
            <p className="text-foreground/80">
              That is the whole idea. Catch the thing that looks fine but is not.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}
