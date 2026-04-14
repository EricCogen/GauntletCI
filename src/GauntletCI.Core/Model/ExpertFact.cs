// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Model;

/// <summary>Expert fact attached to a finding by the LLM adjudicator.</summary>
public sealed record ExpertFact(string Content, string Source, float Score);
