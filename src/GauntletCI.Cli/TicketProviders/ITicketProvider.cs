// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;
namespace GauntletCI.Cli.TicketProviders;

public interface ITicketProvider
{
    string ProviderName { get; }
    /// <summary>Returns true if required env vars/tokens are present.</summary>
    bool IsAvailable { get; }
    /// <summary>Fetches ticket info by key; returns null if not found or on error.</summary>
    Task<TicketInfo?> FetchAsync(string issueKey, CancellationToken ct = default);
}
