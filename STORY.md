# Why I Built GauntletCI

GauntletCI is a diff-first verification tool for C#/.NET teams. It identifies risky behavioral changes and weak validation, the specific categories of mistakes that pass tests and code review but still break production.

Linters ask whether code follows rules. Test suites ask whether known expectations still pass. GauntletCI asks a different question: did this change introduce behavior that hasn't been validated?

## 1. The Lego Conundrum

You're searching for a specific piece that you know is there. You've seen it before, your little brother used it on his "airplane" last week, so you keep searching and swiping through that giant tub of Legos you've been carefully curating for years. Finally, you have to look away, and on your next swipe, poof, like magic, it's suddenly right in front of you. 



Most of the bugs I've shipped in twenty years of .NET production reveal themselves the same way. You look right at them and can't see them. This isn't because you're careless, but because your brain already decided the answer and didn't have the decency to let you in on that fact.

In the third quarter of 2025, I received a new offer: a thirty percent pay increase, a greenfield codebase, and a chance to start clean. I walked in with no legacy debt, no inherited mess, and no excuses.

Within weeks, the illusion of a fresh start shattered, and I found myself staring at the same recurring patterns of mistakes that I had spent years trying to get past. 

They weren't the same bugs, but they were the same shape of mistakes. These were changes that passed review and passed tests, then failed loudly in production. New codebase, new team, and a new stack in places, but the same postmortem. It was the guard clause that seemed redundant, the rename that looked like cleanup, or the exception path nobody thought to test. The code was different, but the outcome was identical.

I was tired of being mediocre. I wanted to be better, and I knew I could be. It was time to step up my game.

I spent a long time thinking it was a discipline problem. I told myself I needed to slow down, be more careful, and read the diff one more time. I tried checklists, rubber duck debugging, and even the rule of not shipping on Fridays. 

None of it worked consistently. The failures kept coming, not constantly, but regularly enough to be a pattern. And patterns mean something.

The thing that finally broke the frame was noticing when the mistakes happened. It wasn't when I was being careless, it was when I was being efficient. The bugs I shipped weren't the result of rushing past something I knew mattered. They were the result of my brain quietly deciding something didn't need to be examined, and being wrong.

That's a different problem entirely. Discipline assumes you know where the risk is and are choosing not to look. What I was dealing with was something earlier than that: the risk was invisible at the moment of decision. The assumption had already been made before I was conscious of making it.

You can't fix that with more discipline. You can't will yourself to examine an assumption you don't know you're making. What I needed was a system that doesn't share your blind spots, something external, deterministic, and constitutionally unable to take the same shortcuts your brain does. 

That's when I stopped trying to be more careful and started trying to build something that didn't need to be.

## 2. The Mirror

Late in 2025, I started using LLMs as an adversarial sounding board, not to write code, but to stress-test my reasoning and surface the assumptions I was glossing over.

The first questions I formalized weren't technical. "Am I embarrassing myself?" Then: "Does this actually accomplish what the ticket asked?" Not "does it run," but if the person who wrote that ticket looked at this PR, would they say "yes, that's exactly what I meant"?

That last question was the one that changed everything. It forced me to audit intent, not syntax. The gaps it surfaced were exactly the kind no linter, no static analyzer, and no existing tool in my stack would ever find. 

Over time, those questions became a checklist. The checklist became a habit. The habit became the foundation of GauntletCI.

## 3. The Survival Checklist

The rules weren't style guides. They came from real failures, each one something that broke in production on my watch.

The first few were about context: refresh the branch, re-read the ticket, and re-read your own diff like you're seeing it for the first time. Then validation: are the tests checking intent or just syntax? Then the pride check, which includes two questions that sound simple but aren't: "is this production ready," and "will this embarrass me?" Then came behavioral risk, the secret sauce: "did I unintentionally change something," and "are the edge cases actually handled?"

Twenty rules total. Every one of them came from a production failure I either caused or shipped.

## 4. The First Attempt

My first build was called PreCommitGuard. I took the checklist and turned it into a proof of concept, an LLM-evaluated gate that ran my rules against every diff before commit. 

**It worked for me, really, really well, but it ended up falling apart the moment I stress-tested it seriously.**

The problem cuts to the core: LLMs are probabilistic, and thus may never be truly capable of being deterministic, which in software development is something that simply cannot be. Even their own documentation admits they are non-deterministic, but that is only half the problem. These models are trained on the entirety of the internet, a repository containing vast amounts of high-quality code, but also an ocean of misinformation, outdated patterns, and specious logic. 

When you rely on a "black box" that has effectively learned from every bad habit on the web, you aren't just getting an unreliable judge; you are getting a mirror that reflects the industry's worst instincts. If the whole point of the tool is to catch the assumptions you didn't know you were making, you cannot build it on a foundation that hides its reasoning and inherits the fallacies of its training data.

I needed to rethink the architecture entirely.

## 5. The Insight

I stepped away from the IDE. When I'm stuck, I like to take a walk and let my mind go sideways, picking on ideas that have been floating around for years.

That's when I started sketching out what I had thought was a completely unrelated tool for tabletop RPG players. Anyone who ran a D&D campaign knows the problem: your rogue swore a blood oath to the thieves' guild in session four, and by session twelve you've forgotten. Now you're writing them acting like they're a free agent. The story breaks, the players notice, and the DM scrambles.

The consistency engine I was sketching was trying to catch the gap between what a character was and what they were doing now, flagging the moments where accumulated history contradicted current behavior. 

I was halfway through the design when it hit me: that's the same problem. 

Both tools were trying to externalize judgment. One was catching places where a character's behavior diverged from their established contract. The other was catching places where a code change diverged from the system's established behavior. The diff was the character sheet, the behavioral rules were the campaign history, and the gap between intent and execution was identical.

That parallel gave me the architecture. Not an LLM deciding what it thinks, but a deterministic system that doesn't share your blind spots, running structured rules against the change itself, flagging the divergence before it ships.

## 6. The Rebuild: GauntletCI

I rebuilt from scratch with a different foundation. 

GauntletCI is not an AI code reviewer, and it is not a style checker. It is a pessimistic verification system for risky diffs. Its job is to ask whether a change altered behavior, weakened validation, missed an edge case, or introduced risk that ordinary review can easily overlook.



The detection engine is built on deterministic Roslyn analyzers that run locally on your machine. We decided to keep AI/LLM as an optional tool to help translate our hard, binary findings into plain English. The AI does not decide what gets flagged. The rules do, every time, the same way, on every diff. 

By pulling the decision-making logic out of the black box and into a deterministic codebase, we ensure that every diff is validated the same way, every time. You push a commit, and it flags the behavior change you didn't mean to make before anyone sees the PR. You are relying on rigorous, rule-based verification rather than the unpredictable "vibes" of a model trained on the internet's noise.

## 7. Who This Is For

GauntletCI is for engineers who already know how to write code, but still know the feeling of missing something they should have caught. 

It's for teams that have learned the hard way that green tests, passing reviews, and clean linters do not always mean a change is safe. It's for the part of software engineering that still depends too much on memory, attention, and hope.

## 8. Final Note

Experience doesn't eliminate mistakes, it changes their shape. 

The most dangerous bugs aren't the ones you don't understand. They're the cognitive skips, the subtle doubts you almost investigated and let slide. The gap between what you meant to build and what you actually built is filled with assumptions nobody examined and behaviors nobody validated. 

GauntletCI is my answer to that gap.

---

**Follow the Build**

GauntletCI is in active development. If this problem feels familiar, follow the build, inspect the technical docs, or try it on your next diff.

**GitHub:** [github.com/EricCogen/GauntletCI](https://github.com/EricCogen/GauntletCI)  
**X (Twitter):** [@GauntletCI_BCRV](https://twitter.com/GauntletCI_BCRV)  
**Site:** [GauntletCI.com](https://gauntletci.com)

**Eric I. Cogen, Founder GauntletCI**
