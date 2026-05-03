// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis.Enrichment;
using GauntletCI.Cli.Enrichment.Stages;
using GauntletCI.Llm;

namespace GauntletCI.Cli.Enrichment;

/// <summary>
/// Factory for building configured enrichment pipelines with various combinations of enrichers.
/// </summary>
public static class EnrichmentPipelineFactory
{
    /// <summary>
    /// Creates the default production pipeline with all available enrichers.
    /// Includes: CodeSnippetEnricher (always) + LlmExplanationEnricher (if LLM available).
    /// </summary>
    /// <param name="llmEngine">Optional LLM engine for natural-language explanations. If null, LlmExplanationEnricher is skipped.</param>
    /// <returns>A fully configured EnrichmentPipeline ready for use.</returns>
    public static EnrichmentPipeline CreateDefault(ILlmEngine? llmEngine = null)
    {
        var enrichers = new List<IFindingEnricher>
        {
            // Always-available enrichers (no dependencies)
            new CodeSnippetEnricher(),
        };

        // Add LLM enricher only if engine is provided and available
        if (llmEngine?.IsAvailable == true)
        {
            enrichers.Add(new LlmExplanationEnricher(llmEngine));
        }

        return new EnrichmentPipeline(enrichers);
    }

    /// <summary>
    /// Creates a minimal pipeline with only code snippet extraction (no external dependencies).
    /// Useful for offline scenarios or when LLM service is unavailable.
    /// </summary>
    /// <returns>An EnrichmentPipeline with only CodeSnippetEnricher.</returns>
    public static EnrichmentPipeline CreateMinimal()
    {
        return new EnrichmentPipeline(new[] { new CodeSnippetEnricher() });
    }

    /// <summary>
    /// Creates a pipeline configured for a specific set of enricher types.
    /// Allows fine-grained control over which enrichers are included.
    /// </summary>
    /// <param name="options">Flags indicating which enrichers to include.</param>
    /// <param name="llmEngine">Optional LLM engine. Required if EnricherOptions.LlmExplanation is set.</param>
    /// <returns>An EnrichmentPipeline with the specified enrichers.</returns>
    /// <exception cref="InvalidOperationException">Thrown if LlmExplanation is requested but llmEngine is null.</exception>
    public static EnrichmentPipeline Create(EnricherOptions options, ILlmEngine? llmEngine = null)
    {
        var enrichers = new List<IFindingEnricher>();

        if ((options & EnricherOptions.CodeSnippet) != 0)
        {
            enrichers.Add(new CodeSnippetEnricher());
        }

        if ((options & EnricherOptions.LlmExplanation) != 0)
        {
            if (llmEngine == null)
                throw new InvalidOperationException("LlmExplanation enricher requested but llmEngine is null.");
            if (llmEngine.IsAvailable)
                enrichers.Add(new LlmExplanationEnricher(llmEngine));
        }

        if (enrichers.Count == 0)
            throw new InvalidOperationException("At least one enricher must be selected.");

        return new EnrichmentPipeline(enrichers);
    }
}

/// <summary>
/// Flags for selecting which enrichers to include in a pipeline.
/// Can be combined with bitwise OR: EnricherOptions.CodeSnippet | EnricherOptions.LlmExplanation
/// </summary>
[Flags]
public enum EnricherOptions
{
    /// <summary>None (do not use directly).</summary>
    None = 0,

    /// <summary>Include CodeSnippetEnricher (always available, no dependencies).</summary>
    CodeSnippet = 1 << 0,

    /// <summary>Include LlmExplanationEnricher (requires ILlmEngine).</summary>
    LlmExplanation = 1 << 1,

    /// <summary>Include all available enrichers (CodeSnippet + LlmExplanation if LLM available).</summary>
    All = CodeSnippet | LlmExplanation,
}
