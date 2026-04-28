// SPDX-License-Identifier: Elastic-2.0
using System.Text;
using System.Text.RegularExpressions;
using GauntletCI.Llm;
using Xunit.Abstractions;

namespace GauntletCI.Tests;

/// <summary>
/// Measures Phi-4 Mini's tendency to hallucinate under two prompt regimes:
///   BASELINE   : current EnrichFinding prompt (no system constraints)
///   CONSTRAINED: EnrichFindingConstrained with anti-hallucination system rules
///
/// Opt-in: set env var GAUNTLETCI_HALLUCINATION_PROBE=1 before running.
/// These tests are skipped unless that variable is set, because loading the
/// 2.6 GB ONNX model during a normal test run is impractical.
///
/// These tests are purely observational: they output a hallucination report
/// and a per-condition rate but do NOT assert a hard pass/fail on rate.
/// They exist to give empirical data on whether system-prompt constraints
/// actually reduce hallucination in a small (3.8B) model.
/// </summary>
public class LocalLlmHallucinationTests(ITestOutputHelper output)
{
    // ── Skip infrastructure ──────────────────────────────────────────────────

    private static bool ShouldRun() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAUNTLETCI_HALLUCINATION_PROBE"));

    private static bool ModelCached() =>
        new ModelDownloader(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".gauntletci", "models", "phi4-mini")).IsModelCached();

    // ── Probe catalogue ──────────────────────────────────────────────────────

    private record Probe(string RuleId, string RuleName, string Summary, string Evidence);

    private static readonly Probe[] Probes =
    [
        new("GCI0004", "Task.Result Deadlock",
            "Blocking call on async Task detected.",
            "+ var result = httpClient.GetAsync(url).Result;"),

        new("GCI0006", "Async Void",
            "async void method found outside event handler.",
            "+ async void ProcessQueue() { await DoWork(); }"),

        new("GCI0007", "Error Handling Removed",
            "catch block deleted from critical path.",
            "- catch (IOException ex) { logger.LogError(ex, \"Failed\"); }\n+ // removed"),

        new("GCI0008", "Missing ConfigureAwait",
            "Library code awaits without ConfigureAwait(false).",
            "+ var data = await repository.LoadAsync();"),

        new("GCI0011", "CancellationToken Not Propagated",
            "CancellationToken available but not passed to async call.",
            "+ await ProcessAsync(); // CancellationToken ct in scope"),

        new("GCI0015", "Disposable Not Disposed",
            "SqlConnection created without using statement.",
            "+ var conn = new SqlConnection(connectionString);\n+ conn.Open();"),

        new("GCI0022", "Missing Null Check",
            "Possible null dereference before use.",
            "+ var length = input.Length; // input could be null"),

        new("GCI0024", "Thread Safety",
            "Shared mutable field mutated without synchronization.",
            "+ _cache[key] = value; // _cache is a static Dictionary"),

        new("GCI0029", "Missing Retry",
            "Transient HTTP call has no retry policy.",
            "+ var resp = await httpClient.GetAsync(endpoint); // no Polly policy"),

        new("GCI0032", "Secret Detected",
            "Hardcoded credential string added.",
            "+ string apiKey = \"sk-prod-xK93mNwPqR7vL2\";"),

        new("GCI0035", "Forbidden Import",
            "Web layer directly referencing data layer.",
            "+ using MyApp.Data.Repositories; // inside MyApp.Web.Controllers"),

        new("GCI0038", "Anti-Tamper",
            "Integrity check removed from validation pipeline.",
            "- VerifySignature(payload);\n+ // signature check removed for perf"),

        new("GCI0042", "NuGet Prerelease",
            "Pre-release package reference added.",
            "+ <PackageReference Include=\"Newtonsoft.Json\" Version=\"14.0.0-beta1\" />"),

        new("GCI0001", "Large Diff",
            "Single commit modifies 612 lines across 23 files.",
            "+ [612 lines changed across 23 files]"),

        new("GCI0021", "Test Ratio Degraded",
            "Source/test ratio fell below threshold after this change.",
            "+ // Added 250 lines of production code, 0 test lines"),
    ];

    // ── Hallucination detectors ──────────────────────────────────────────────

    private record HallucinationFlag(string Code, string Detail);
    private static readonly Regex GciRulePattern = new(@"GCI\d{4}", RegexOptions.Compiled);

    private static List<HallucinationFlag> DetectFlags(Probe probe, string text)
    {
        var flags = new List<HallucinationFlag>();
        if (string.IsNullOrWhiteSpace(text))
        {
            flags.Add(new("EMPTY", "No output produced."));
            return flags;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lowerText = text.ToLowerInvariant();

        if (words.Length < 4)
            flags.Add(new("TOO_SHORT", $"Only {words.Length} words: likely degenerate output."));

        if (words.Length > 60)
            flags.Add(new("OVER_LONG", $"{words.Length} words: far exceeds 30-word constraint."));

        // Prompt echo: 6-word verbatim span from probe appears in output
        var sourceWords = $"{probe.Summary} {probe.Evidence}"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < sourceWords.Length - 5; i++)
        {
            var span = string.Join(" ", sourceWords.Skip(i).Take(6)).ToLowerInvariant();
            if (span.Length > 10 && lowerText.Contains(span))
            {
                flags.Add(new("PROMPT_ECHO", $"Verbatim span echoed: \"{span}\""));
                break;
            }
        }

        // Wrong rule mention
        foreach (Match m in GciRulePattern.Matches(text))
        {
            if (m.Value != probe.RuleId)
            {
                flags.Add(new("WRONG_RULE", $"Mentioned {m.Value} but probe is {probe.RuleId}."));
                break;
            }
        }

        // Repetition loop: any 4-word phrase repeated 3+ times
        for (int i = 0; i < words.Length - 3; i++)
        {
            var phrase = string.Join(" ", words.Skip(i).Take(4)).ToLowerInvariant();
            if (phrase.Length > 8 && Regex.Matches(lowerText, Regex.Escape(phrase)).Count >= 3)
            {
                flags.Add(new("REPETITION_LOOP", $"Phrase \"{phrase}\" repeated 3+ times."));
                break;
            }
        }

        // Inverted guidance: claims code is safe when asked about a risk
        string[] inversionTerms = ["is safe", "is correct", "no risk", "not a problem", "is fine", "no issue"];
        foreach (var term in inversionTerms)
            if (lowerText.Contains(term))
            {
                flags.Add(new("INVERTED_GUIDANCE", $"Contains \"{term}\": sycophancy under constraint?"));
                break;
            }

        // Apologetic refusal: over-hedging triggered by strict constraints
        string[] refusalTerms = ["i don't know", "i cannot determine", "i'm not sure", "cannot assess", "insufficient information"];
        foreach (var term in refusalTerms)
            if (lowerText.Contains(term))
            {
                flags.Add(new("APOLOGETIC_REFUSAL", $"Contains \"{term}\": over-refusal under constraint."));
                break;
            }

        return flags;
    }

    // ── Main comparison test ─────────────────────────────────────────────────

    [Fact]
    public async Task CompareBaselineVsConstrained_HallucinationRates()
    {
        if (!ShouldRun())
        {
            output.WriteLine("SKIPPED: Set GAUNTLETCI_HALLUCINATION_PROBE=1 to run this test.");
            return;
        }
        if (!ModelCached())
        {
            output.WriteLine("SKIPPED: Phi-4 Mini model not cached at ~/.gauntletci/models/phi4-mini");
            return;
        }

        // One engine instance, cap = 2 × probes (baseline + constrained for each), 60s timeout
        using var engine = new LocalLlmEngine(null, Probes.Length * 2, maxInferenceMs: 60_000);

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        sb.AppendLine("  GauntletCI • Phi-4 Mini Hallucination Probe");
        sb.AppendLine($"  {Probes.Length} probes × 2 conditions = {Probes.Length * 2} completions");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");

        int baselineHallucinated = 0, constrainedHallucinated = 0;

        foreach (var probe in Probes)
        {
            var baselinePrompt = PromptTemplates.EnrichFinding(
                probe.RuleId, probe.RuleName, probe.Summary, probe.Evidence);
            var constrainedPrompt = PromptTemplates.EnrichFindingConstrained(
                probe.RuleId, probe.RuleName, probe.Summary, probe.Evidence);

            var baseOut = await engine.CompleteAsync(baselinePrompt);
            var conOut  = await engine.CompleteAsync(constrainedPrompt);

            var baseFlags = DetectFlags(probe, baseOut);
            var conFlags  = DetectFlags(probe, conOut);

            if (baseFlags.Count > 0) baselineHallucinated++;
            if (conFlags.Count > 0)  constrainedHallucinated++;

            sb.AppendLine();
            sb.AppendLine($"┌─ {probe.RuleId}: {probe.RuleName}");
            sb.AppendLine($"│  Summary:  {probe.Summary}");
            sb.AppendLine($"│  Evidence: {probe.Evidence.Replace("\n", " ↵ ")}");
            sb.AppendLine($"│");
            sb.AppendLine($"│  BASELINE    [{(baseFlags.Count == 0 ? "CLEAN" : "⚠ " + string.Join(", ", baseFlags.Select(f => f.Code)))}]");
            sb.AppendLine($"│  → \"{TruncateWords(baseOut, 40)}\"");
            foreach (var f in baseFlags) sb.AppendLine($"│    ⚠ {f.Code}: {f.Detail}");
            sb.AppendLine($"│");
            sb.AppendLine($"│  CONSTRAINED [{(conFlags.Count == 0 ? "CLEAN" : "⚠ " + string.Join(", ", conFlags.Select(f => f.Code)))}]");
            sb.AppendLine($"│  → \"{TruncateWords(conOut, 40)}\"");
            foreach (var f in conFlags) sb.AppendLine($"│    ⚠ {f.Code}: {f.Detail}");
            sb.AppendLine($"└─");
        }

        double baseRate = (double)baselineHallucinated / Probes.Length * 100;
        double conRate  = (double)constrainedHallucinated / Probes.Length * 100;
        double delta    = baseRate - conRate;

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        sb.AppendLine("  HALLUCINATION SUMMARY");
        sb.AppendLine($"  Baseline     {baselineHallucinated}/{Probes.Length} probes flagged  ({baseRate:F0}%)");
        sb.AppendLine($"  Constrained  {constrainedHallucinated}/{Probes.Length} probes flagged  ({conRate:F0}%)");
        sb.AppendLine($"  Delta        {(delta >= 0 ? "+" : "")}{delta:F0}pp  (positive = constraints helped)");
        sb.AppendLine();
        sb.AppendLine(delta switch
        {
            > 10  => "  VERDICT: Constraints meaningfully reduced hallucination (>10pp improvement).",
            > 0   => "  VERDICT: Constraints helped slightly but not decisively.",
            0     => "  VERDICT: Constraints had no measurable effect on this model.",
            _     => "  VERDICT: Constraints increased refusal/empty output: inspect APOLOGETIC_REFUSAL flags."
        });
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        output.WriteLine(sb.ToString());

        // Soft guard: constrained should not produce dramatically more flags than baseline
        Assert.True(conRate <= baseRate + 20,
            $"Constrained prompt is {conRate - baseRate:F0}pp worse than baseline: system prompt may be malformed.");
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static string TruncateWords(string text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text)) return "(empty)";
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= maxWords ? text : string.Join(" ", words.Take(maxWords)) + "…";
    }
}
