// SPDX-License-Identifier: Elastic-2.0
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace GauntletCI.Cli.LlmDaemon;

/// <summary>
/// Session token and pipe ACL helpers for the local LLM daemon.
/// </summary>
internal static class LlmDaemonAuth
{
    internal static string TokenFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "llm-daemon.token");

    internal static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    internal static async Task PersistTokenAsync(string token, CancellationToken ct = default)
    {
        var path = TokenFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await File.WriteAllTextAsync(path, token, ct).ConfigureAwait(false);
        TryRestrictTokenFile(path);
    }

    internal static void DeleteTokenFile()
    {
        try { File.Delete(TokenFilePath); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LlmDaemonAuth: Failed to delete token file: {ex.Message}");
        }
    }

    internal static string? TryReadPersistedToken()
    {
        try
        {
            if (!File.Exists(TokenFilePath))
                return null;

            var token = File.ReadAllText(TokenFilePath).Trim();
            return string.IsNullOrEmpty(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }

    internal static bool IsValidToken(string? expected, string? provided)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    internal static NamedPipeServerStream CreateServerStream(string pipeName)
    {
        if (OperatingSystem.IsWindows())
        {
            var security = CreateCurrentUserPipeSecurity();
            return NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity: security);
        }

        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    [SupportedOSPlatform("windows")]
    private static PipeSecurity CreateCurrentUserPipeSecurity()
    {
        var security = new PipeSecurity();
        var user = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Current Windows identity has no user SID.");

        security.AddAccessRule(new PipeAccessRule(
            user,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));
        security.SetOwner(user);
        return security;
    }

    private static void TryRestrictTokenFile(string path)
    {
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                return;
            }

            if (!OperatingSystem.IsWindows())
                return;

            var info = new FileInfo(path);
            var acl = info.GetAccessControl();
            acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            acl.AddAccessRule(new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().User!,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            info.SetAccessControl(acl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LlmDaemonAuth: Failed to restrict token file permissions: {ex.Message}");
        }
    }
}
