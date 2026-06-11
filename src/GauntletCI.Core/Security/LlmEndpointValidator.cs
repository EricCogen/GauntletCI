// SPDX-License-Identifier: Elastic-2.0
using System.Net;
using System.Net.Sockets;

namespace GauntletCI.Core.Security;

/// <summary>
/// Validates Ollama / OpenAI-compatible LLM base URLs before outbound requests.
/// MCP uses loopback-only; repo config may allow private LAN hosts for local Ollama.
/// </summary>
public static class LlmEndpointValidator
{
    private static readonly IPAddress CloudMetadataAddress = new(new byte[] { 169, 254, 169, 254 });

    /// <summary>
    /// MCP Ollama URL: HTTP(S) to loopback only (localhost / 127.0.0.1 / ::1).
    /// </summary>
    public static bool TryValidateMcpOllamaBaseUrl(string? url, out string? error)
    {
        if (url is null)
        {
            error = "URL is empty.";
            return false;
        }

        return TryValidateOllamaBaseUrl(url, allowPrivateLan: false, out error);
    }

    /// <summary>
    /// Repo-config Ollama URL: loopback or RFC1918 private LAN; blocks cloud metadata and link-local.
    /// </summary>
    public static bool TryValidateConfigOllamaBaseUrl(string? url, out string? error)
    {
        if (url is null)
        {
            error = "URL is empty.";
            return false;
        }

        return TryValidateOllamaBaseUrl(url, allowPrivateLan: true, out error);
    }

    private static bool TryValidateOllamaBaseUrl(string? url, bool allowPrivateLan, out string? error)
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

        if (uri.Scheme is not ("http" or "https"))
        {
            error = "URL must use HTTP or HTTPS.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "URL must not contain embedded credentials.";
            return false;
        }

        var host = uri.IdnHost.ToLowerInvariant();

        if (IsCloudMetadataHost(host))
        {
            error = "URL host is not allowed.";
            return false;
        }

        if (IsLoopbackHost(host))
            return true;

        if (!allowPrivateLan)
        {
            error = "MCP Ollama URL must target loopback (localhost or 127.0.0.1).";
            return false;
        }

        if (!IPAddress.TryParse(host, out var ip))
        {
            error = "Config Ollama URL must be an IP address or loopback hostname.";
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork && IsBlockedLanIPv4(ip.GetAddressBytes()))
        {
            error = "URL host is not allowed.";
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && IsBlockedLanIPv6(ip.GetAddressBytes()))
        {
            error = "URL host is not allowed.";
            return false;
        }

        return true;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("[::1]", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        return ip.Equals(IPAddress.Loopback) || ip.Equals(IPAddress.IPv6Loopback);
    }

    private static bool IsCloudMetadataHost(string host)
    {
        if (host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(host, out var ip) && ip.Equals(CloudMetadataAddress);
    }

    private static bool IsBlockedLanIPv4(byte[] bytes) =>
        bytes[0] switch
        {
            169 when bytes[1] == 254 => true,
            127 => true,
            _ => false,
        };

    private static bool IsBlockedLanIPv6(byte[] bytes) =>
        (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80);
}
