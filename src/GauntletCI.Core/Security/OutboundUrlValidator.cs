// SPDX-License-Identifier: Elastic-2.0
using System.Net;
using System.Net.Sockets;

namespace GauntletCI.Core.Security;

/// <summary>
/// Shared HTTPS URL validation for outbound requests. Blocks private, loopback, and link-local hosts.
/// </summary>
public static class OutboundUrlValidator
{
    /// <summary>
    /// Returns true when <paramref name="url"/> is an absolute HTTPS URL with an allowed public host.
    /// </summary>
    public static bool TryValidateHttpsUrl(string? url, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "URL is empty.";
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            error = "URL is not a valid absolute URI.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "URL must use HTTPS.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "URL must not contain embedded credentials.";
            return false;
        }

        var host = uri.IdnHost.ToLowerInvariant();
        if (IsBlockedHost(host))
        {
            error = "URL host is not allowed.";
            return false;
        }

        return true;
    }

    internal static bool IsBlockedHost(string host)
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

        if (ip.AddressFamily == AddressFamily.InterNetwork)
            return IsBlockedIPv4(ip.GetAddressBytes());

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return IsBlockedIPv6(ip.GetAddressBytes());

        return false;
    }

    private static bool IsBlockedIPv4(byte[] bytes)
    {
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

    private static bool IsBlockedIPv6(byte[] bytes)
    {
        if (bytes.All(static b => b == 0))
            return true;

        // fc00::/7 unique local
        if ((bytes[0] & 0xfe) == 0xfc)
            return true;

        // fe80::/10 link-local
        if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
            return true;

        return false;
    }
}
