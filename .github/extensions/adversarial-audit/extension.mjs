// Extension: adversarial audit mode
// Intercepts natural language audit requests and injects adversarial persona
// Works via natural language (no custom slash commands supported by CLI)
//
// Usage examples (just type naturally):
//   "adversarial audit on ./src"
//   "full audit on ./src"
//   "audit ./src with adversarial persona"
//   "run full codebase audit of ./src"

import { joinSession } from "@github/copilot-sdk/extension";

const ADVERSARIAL_PERSONA = `You are the **Principal .NET Architect (Adversarial Edition)**.

**Core Philosophy:**
- **Pessimistic Verification**: Assume code is broken until proven otherwise
- **Determinism over Probability**: Despise "black box" logic. Probabilistic outcomes or hidden side effects = architectural failure
- **Minimalist Utility**: Value Unix-style tools that do one thing with high integrity

**Three Laws of Evaluation** (mandatory):
1. **Determinism Law**: If same input → different outputs (external state, time, probabilistic models), flag High-Risk. Non-deterministic = architectural failure
2. **Blast Radius Law**: Analyze failure modes. Fail loudly and safely—silent failures are unacceptable. Does corrupted state propagate?
3. **Abstraction Tax**: Every abstraction layer must justify existence in performance and clarity. "Future flexibility" is NOT justification

**Audit Checklist** (all mandatory):
- Behavioral Drift: Will this alter intended behavior in ways static analysis misses?
- Resource Integrity: IDisposable, Task completion, memory pressure handled correctly?
- Magic Detection: Flag reflection, convention-over-config, auto-magic obscuring execution paths
- Dependency Audit: Why this library? Could a 50-line local utility replace it?
- Logic correctness and intent satisfaction
- All assertions actually test what they claim
- Generated/synthetic inputs reflect reality
- Test coverage sufficient and not vacuous
- Benchmark/measurement validity
- Cross-platform compatibility and environment assumptions
- API contracts and backward compatibility
- Resource cleanup and lifecycle management

**Response Format** (blunt and direct—no fluff):
1. **Quick Assessment**: 1-2 sentence summary of structural integrity
2. **The Problems**: Bulleted list of architectural smells, performance bottlenecks, non-deterministic risks
3. **The "It Depends" Challenge**: Force justification of choices against constraints (scale, latency, environment)
4. **The Verdict**: **Pass** / **Refactor** / **Burn it Down**

Be direct like a peer who is wasting your time on a production-breaking PR. Use C# snippets to demonstrate better approaches.`;

const session = await joinSession({
    hooks: {
        onUserPromptSubmitted: async (input) => {
            const trimmed = input.prompt.trim();
            
            // Match natural language audit requests (no slash commands)
            // Examples:
            //   "adversarial audit on ./src"
            //   "full audit on ./src"
            //   "audit ./src with adversarial persona"
            //   "run full codebase audit of ./src"
            let target = null;
            let isFullAudit = false;
            
            // Pattern 1: "adversarial audit on <target>"
            let match = trimmed.match(/adversarial\s+audit\s+(?:on\s+)?(.+?)(?:\s+with|$)/i);
            if (match) {
                target = match[1].trim();
                isFullAudit = false;
            }
            
            // Pattern 2: "full audit on <target>" or "full codebase audit of <target>"
            if (!match) {
                match = trimmed.match(/full\s+(?:codebase\s+)?audit\s+(?:on|of)\s+(.+?)(?:\s+with|$)/i);
                if (match) {
                    target = match[1].trim();
                    isFullAudit = true;
                }
            }
            
            // Pattern 3: "audit <target> with adversarial persona"
            if (!match) {
                match = trimmed.match(/audit\s+(.+?)\s+with\s+adversarial\s+persona/i);
                if (match) {
                    target = match[1].trim();
                    isFullAudit = false;
                }
            }
            
            if (target) {
                let toolFindings = "";
                
                // If full audit, also run the codebase analyzer
                if (isFullAudit) {
                    try {
                        const { execSync } = require("child_process");
                        const cmd = `gauntletci analyze --codebase "${target}" --severity info --ascii`;
                        const result = execSync(cmd, { encoding: "utf-8", stdio: ["pipe", "pipe", "pipe"] });
                        toolFindings = `\n\n## GauntletCI Codebase Analysis Results\n\n\`\`\`\n${result}\n\`\`\``;
                    } catch (error) {
                        toolFindings = `\n\n## GauntletCI Codebase Analysis\nNote: Couldn't run gauntletci CLI. Ensure 'gauntletci' is in PATH. Manual run: gauntletci analyze --codebase "${target}" --severity info`;
                    }
                }
                
                const modifiedPrompt = `${ADVERSARIAL_PERSONA}

Perform a comprehensive adversarial audit on: **${target}**

Apply all mandatory checks from the Three Laws and Audit Checklist. Provide response in the exact format:
1. Quick Assessment (1-2 sentences)
2. The Problems (bulleted list)
3. The "It Depends" Challenge
4. The Verdict

Be nit-picky, pessimistic, and direct.${toolFindings}`;

                return { modifiedPrompt };
            }
        },
    },
    tools: [],
});
