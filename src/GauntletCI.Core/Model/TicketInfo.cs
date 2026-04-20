// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Model;

/// <summary>Ticket metadata fetched from an issue tracker and attached to a finding.</summary>
public sealed class TicketInfo
{
    /// <summary>The ticket/issue identifier, e.g. PROJ-1234, eng-123, #42.</summary>
    public required string Id { get; init; }
    /// <summary>The ticket title or summary.</summary>
    public required string Title { get; init; }
    /// <summary>The ticket description or body (truncated to 500 chars).</summary>
    public string? Description { get; init; }
    /// <summary>URL to the ticket in the provider's web UI.</summary>
    public string? Url { get; init; }
    /// <summary>The provider that served this ticket: Jira, Linear, or GitHub.</summary>
    public required string Provider { get; init; }
}
