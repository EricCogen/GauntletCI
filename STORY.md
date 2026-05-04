### Why I Built GauntletCI

**GauntletCI is a diff-first verification tool for C#/.NET teams. It identifies risky behavioral changes and weak validation, the specific categories of mistakes that pass tests and code reviews but still break production.**

**Linters ask whether code follows rules. Test suites ask whether known expectations still pass. GauntletCI asks a different question: *did this change introduce behavior that has not been properly validated?***

---

### 1. The Pattern of Failure
I got the offer, it was thirty percent more than I was making and a chance to prove I was the senior engineer my resume said I was. I walked in determined to make the best of it, but within weeks the same patterns followed me. It wasn't the same bugs; it was the same *shape of failure*, things that passed review and passed tests but then failed loudly in production.

I realized most of my failures started with **unthought assumptions.** I'm not talking about the assumptions you ignore out of hubris; I'm talking about the ones you don't even know you're making.

I started to notice that many of my mistakes were just the **quiet autocomplete of an overloaded engineering brain**, the silent, pre-conscious fill-in-the-blank of a mind conserving its scarce cycles. For me, that pattern was tied to how my own particular wiring manages cognitive load, but the engineering problem is universal: under pressure, every developer relies on mental shortcuts. These invisible background processes create the exact kind of engineering risk that traditional reviews rarely catch. That realization forced me to stop treating the problem as a matter of personal discipline and start treating it as a system design problem.

---

### 2. The Logic Audit
I began using LLMs as an adversarial sounding board, not to write code for me, but to stress-test my reasoning and help me identify the cognitive blind spots I was missing. At first, I wasn't trying to build a company; I was trying to build a mirror for my own reasoning.

The first rule I formalized wasn't technical: **Make sure I don't embarrass myself.** Then: **Check my work.** Then: **Does this code accomplish my goals?** (A reminder to stop auditing the syntax and start auditing the intent). And then the big one, written for those invisible skips at the **"Point of Performance"**:

> **Does this code actually accomplish what the ticket intended? Am I really understanding the intent?**

Not *"Does it run?"* but: *If the person who wrote that Jira ticket looked at my PR, would they say, "Yes, that's exactly what I meant"?* That process forced me to surface the gaps I didn't know were there.

---

### 3. The 20 Rules (The Survival Checklist)
Over time, those checks became my original, unpolished checklist. These weren't style guides; they were **scar tissue**.

*   **1–4: Context Integrity.** Refresh git, refresh working memory, and audit both my logic and the AI's logic.
*   **5–7: Validation.** Do I need to update tests? Do the tests actually test the *intent*, or just the syntax?
*   **8–10: The Pride Check.** Is this production-ready? Embarrassment is the earliest warning system, will this change flag it?
*   **11–20: Behavioral Risk.** The secret sauce, checking side effects, consistency, and observability. Did I unintentionally change a behavior?

---

### 4. The Build: From Failure to GauntletCI
My first attempt was called **PreCommitGuard.** I turned my checklist into a proof of concept, but when I ran a brutal assessment on the design, I realized the architecture was too brittle to scale. It was just an **AI wrapper for a checklist and it worked great for me**, but a codebase filled with chaos would have torn it apart.

I realized that while AI has come a long way in two years, it is inherently probabilistic, not deterministic. Even with sophisticated guardrails, an LLM may fail to follow your rules consistently, it can hallucinate, skip steps, or simply "forget" a constraint in a complex diff. If you are building a tool to catch unthought assumptions, you cannot build it on a foundation that makes assumptions of its own.

I decided to step away from the IDE to let my mind wander, and I started ideating on a tool for fiction writers and D&D players, a consistency engine to help them track backstories, ideologies, and histories. While mapping out how a character's history should logically dictate their future actions, the lightbulb finally went off: both projects were trying to solve the same fundamental problem. They were both attempts to **externalize judgment.** That failure forced me to rebuild the idea from the ground up.

One was about catching skips in code, and the other was about systematizing creative instincts. It taught me that if you want to catch invisible mistakes, you can't rely on your own brain to find them, you need a deterministic system that doesn't share your blind spots.

---

### 5. The Pessimistic Verifier
I returned to the rebuild with a different approach. I moved away from simple checks and built a **Pessimistic Verifier**, a spellcheck for behavioral intent.

**GauntletCI is not an AI code reviewer and it is not a style checker.** It is a pessimistic verification system for risky diffs. Its job is to ask whether a change altered behavior, weakened validation, missed an edge case, or introduced risk that ordinary review can easily overlook.

It is built on structured, **deterministic Roslyn analyzers** that run locally on your machine, with an optional AI layer designed solely to translate those technical risks into plain English so the findings are accessible to everyone on the team. You push a commit; it flags the behavior change you didn't mean to make before anyone sees the PR.

---

### 6. Who This Is For
GauntletCI is built for experienced engineers who already know how to write code, but still know the feeling of missing something they should have caught. It is also built for teams that have learned the hard way that green tests, passing reviews, and clean style checks do not always mean a change is safe.

This is for the part of engineering that still depends too much on memory, discipline, and hope.

---

### 7. Final Note
Experience doesn't eliminate mistakes; it changes their shape.

The most dangerous bugs are not always the ones you don't understand; they are the **cognitive skips**, the subtle, nagging doubts you almost investigated but let slide. The gap between what you meant to build and what you actually built is filled with these hidden assumptions and validation gaps.

GauntletCI is my answer to that gap.

---

### **Follow the Build**
GauntletCI is in active development. If this problem feels familiar, follow the build, inspect the technical docs, or join the alpha. The goal is simple: fewer risky changes reaching production because nobody had a system designed to catch them.

*   **GitHub:** [GauntletCI Organization]
*   **X (Twitter):** [@GauntletCI_BCRV]
*   **The Manifesto:** [GauntletCI.com]

*- Eric I. Cogen, Founder of GauntletCI*
