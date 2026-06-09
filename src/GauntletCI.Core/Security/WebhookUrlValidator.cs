// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Security;

/// <summary>
/// Validates incoming webhook URLs before outbound POST to reduce SSRF risk from config or env vars.
/// </summary>
public static class WebhookUrlValidator
{
    private static readonly string[] TeamsHostSuffixes =
    [
        ".webhook.office.com",
        ".logic.azure.com",
    ];

    /// <summary>
    /// Returns true when <paramref name="url"/> is a valid Slack incoming webhook URL.
    /// </summary>
    public static bool TryValidateSlack(string? url, out string? error)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Webhook URL is empty.";
            return false;
        }

        return TryValidate(url, static host => host.Equals("hooks.slack.com", StringComparison.OrdinalIgnoreCase), out error);
    }

    /// <summary>
    /// Returns true when <paramref name="url"/> is a valid Microsoft Teams incoming webhook URL.
    /// </summary>
    public static bool TryValidateTeams(string? url, out string? error)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Webhook URL is empty.";
            return false;
        }

        if (!TryValidate(url, IsTeamsHostAllowed, out error))
            return false;

        if (!Uri.TryCreate(url!.Trim(), UriKind.Absolute, out var uri))
        {
            error = "Webhook URL is not a valid absolute URI.";
            return false;
        }

        if (uri.Host.Equals("outlook.office.com", StringComparison.OrdinalIgnoreCase)
            && !uri.AbsolutePath.StartsWith("/webhook", StringComparison.OrdinalIgnoreCase))
        {
            error = "Outlook Teams webhook URL must use an /webhook path.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsTeamsHostAllowed(string host)
    {
        if (host.Equals("outlook.office.com", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var suffix in TeamsHostSuffixes)
        {
            if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TryValidate(
        string? url,
        Func<string, bool> hostAllowed,
        out string? error)
    {
        if (!OutboundUrlValidator.TryValidateHttpsUrl(url, out error))
            return false;

        var host = new Uri(url!.Trim()).IdnHost.ToLowerInvariant();
        if (!hostAllowed(host))
        {
            error = "Webhook URL host is not an allowed notification endpoint.";
            return false;
        }

        return true;
    }
}
