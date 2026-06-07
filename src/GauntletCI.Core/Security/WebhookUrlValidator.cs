// SPDX-License-Identifier: Elastic-2.0
using System.Net;

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
        error = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Webhook URL is empty.";
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            error = "Webhook URL is not a valid absolute URI.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Webhook URL must use HTTPS.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "Webhook URL must not contain embedded credentials.";
            return false;
        }

        var host = uri.IdnHost.ToLowerInvariant();
        if (IsBlockedHost(host))
        {
            error = "Webhook URL host is not allowed.";
            return false;
        }

        if (!hostAllowed(host))
        {
            error = "Webhook URL host is not an allowed notification endpoint.";
            return false;
        }

        return true;
    }

    private static bool IsBlockedHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes.All(static b => b == 0))
                return true;

            return bytes[0] switch
            {
                10 => true,
                127 => true,
                169 when bytes[1] == 254 => true,
                172 when bytes[1] is >= 16 and <= 31 => true,
                192 when bytes[1] == 168 => true,
                _ => false,
            };
        }

        return false;
    }
}
