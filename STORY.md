# Why I Built GauntletCI

## The 20 Rules I Wrote to Stop Failing

### 1. The New Job

I got the offer. Thirty percent more than I was making. A fresh start. A chance to prove to them, to myself that I was the senior engineer my resume said I was.

I walked in determined to make the best of it.

Within months, the same patterns followed me.

Not the same bugs. The same *shape* of failure. Subtle things. Things that passed review. Things that passed tests. Things that only surfaced later, quietly, in production.

And here's what I couldn't shake: most of my failures started with **unthought assumptions**.

Not the kind where I said, *"I'll just assume this works."* The kind I didn't even know I was making. I was subconsciously making assumptions. I thought I understood the ticket. I assumed the acceptance criteria covered the edge case. I assumed the existing code worked the way I remembered. I assumed the test actually tested what I thought it did.

These assumptions were invisible to me. They were the water I was swimming in.

I couldn't figure out *how* to get myself past this plateau of mediocrity.

---

### 2. The Spiral

It wasn't one catastrophic mistake. It was a pattern I didn't understand and couldn't break.

I lost confidence. I started using self-deprecating humor to deflect - laughing at myself before anyone else could. *"Classic me. Ship it and pray."*

The humor wore thin.

Eventually, management pulled me aside. Not to fire me. To ask what was going on.

I didn't have a good answer.

I just knew I was letting myself down. Letting my family down. Feeling like an idiot to the people I wanted to impress.

---

### 3. The Conversation That Changed Things

I sat down with an AI. Not for code. For help.

I asked pointed, uncomfortable questions:

- *"How do I stop making the same mistakes?"*
- *"How do I catch the assumptions I don't even know I'm making?"*
- *"How do I make sure my tests actually test what I think they do?"*

The first rule I wrote wasn't technical. It was:

> **Make sure I don't embarrass myself.**

I added another:

> **Check my work.**

Then, for the AI I was increasingly relying on:

> **Check your work.**

And then - this was the big one, I wrote a rule specifically for those unthought assumptions:

> **Does this code actually accomplish what the ticket intended? Am I really understanding the intent of the ticket?**

Not *"Does it run?"* Not *"Does it match the spec I skimmed?"* But: *If the person who wrote that Jira ticket looked at my PR, would they say, "Yes, that's exactly what I meant"?*

That rule forced me to surface the assumptions I didn't know I'd made.

I fed those rules into GitHub Copilot for revisions, streamlining, core truths. I added more as I found the gaps. I kept refining them, over many back-and-forth conversations, until I had a list I could rely on.

---

### 4. The 20 Rules
Over time, those questions became a checklist I could run every time I touched code:

1. Refresh git - keep current changes
2. Refresh working memory
3. Check my work
4. Check your work
5. Do I need to add/remove/update tests?
6. Run all tests
7. Do the tests actually test everything?
8. Does this code accomplish my goals?
9. Is this production ready?
10. Will this embarrass me?
11. Did I introduce breaking changes?
12. Did I unintentionally change behavior?
13. Are edge cases handled?
14. Is error handling correct?
15. Did I add unnecessary complexity?
16. Is this consistent with existing patterns?
17. Did I introduce performance risks?
18. Did I introduce security risks?
19. Did I hardcode anything I shouldn't?
20. Is this observable/debuggable?

These weren't style guides. They were scar tissue.

---

### 5. The First Attempt: PreCommitGuard

I turned those twenty rules into a proof of concept called **PreCommitGuard**. I was excited. I thought I had something real.

So I did what you do when you're serious: I asked the hard questions. I showed the idea to Gemini. I showed it to DeepSeek. I showed it to ChatGPT and I asked those tools for honest brutal feedback with no fluff.

The feedback was clear: the approach I had taken was likely untenable. The architecture wouldn't scale. The methodology wasn't sound.

I was forced to kill the idea.

---

### 6. The Detour

And then, because I didn't know what else to do, I started ideating on something completely different. A tool for game enthusiasts. Dungeons & Dragons players. Fiction writers. Something to help build rich backstories for tertiary characters, complete with ideologies, pathologies, histories, genealogy.

It was interesting. But while I was building it, I found myself asking a deeper question:

> *What do all of these ideas actually have in common?*

The answer surprised me.

Both projects were attempts to **externalize judgment**. One was about catching my own unthought assumptions in code. The other was about systematizing the creative instincts of a writer. Underneath the surface, they were the same problem: *Can we make intuition repeatable? Can we reduce "feel" to structure?*

---

### 7. The Return

That realization sent me back to PreCommitGuard. I knew there was something there - not because I thought it was a clever idea, but because I had **lived proof**. The twenty rules had worked for me, better than expected. They were a proven methodology born from my own failures.

So I forced myself to iterate. To rebuild. To find the architecture that would survive the scrutiny that killed the first version.

That iteration became **GauntletCI**.

---

### 8. From 20 Rules to a System

GauntletCI is the tool I needed during that spiral.

Not a linter. Not a style checker.

A **Pessimistic Verifier**.

A system that assumes I've missed something - and checks anyway.

The 20 rules evolved. They became structured. They became deterministic Roslyn analyzers. They became a corpus of real-world .NET failure modes. They became a local AI that can explain *why* something is risky without sending my code to a cloud I don't control.

But the core remains the same:

> **Fewer "I should have caught that" moments.**

---

### 9. Who This Is For

This isn't for junior developers.

This is for experienced engineers who:

- Have hit a plateau they can't see past
- Have felt their confidence erode for reasons they can't articulate
- Have died a little inside at every standup
- Have stared at a production incident and thought: *"I knew better."*

This is for the part of you that's tired of relying on memory, discipline, and hope.

---

### 10. What I've Learned

Experience doesn't eliminate mistakes. It changes their shape.

The most dangerous bugs aren't the ones you don't understand.

They're the ones you **almost noticed... but didn't.**

The gap between what you *meant* to build and what you *actually* built is filled with unthought assumptions.

GauntletCI is my answer to that moment.

Not perfection. Just fewer 2 AM calls. Fewer quiet walks to the parking lot. Fewer jokes that aren't really jokes. Fewer unthought assumptions.

---

### 11. Final Note

If you've read this far, you probably recognize something in these words.

You're not alone.

And you're not a bad engineer.

You just need a better system.

That's what I'm building.

*- Eric I Cogen, maintainer of GauntletCI*
