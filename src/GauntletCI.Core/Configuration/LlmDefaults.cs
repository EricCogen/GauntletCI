// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Configuration;

/// <summary>
/// Single source of truth for default LLM model names.
/// All code that needs a default Ollama model should reference these constants
/// rather than repeating the string literal.
/// </summary>
public static class LlmDefaults
{
    /// <summary>
    /// Default Ollama model for analysis enrichment, corpus labeling, and embeddings.
    /// Pull with: <c>ollama pull phi4-mini</c>
    /// </summary>
    public const string OllamaModel = "phi4-mini:latest";
}
