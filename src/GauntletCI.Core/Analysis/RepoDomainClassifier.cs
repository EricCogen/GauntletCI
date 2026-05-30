// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.Analysis;

/// <summary>
/// Classifies a repository as web host vs class library to suppress web/DI rule noise (RC-4 / PG-DOMAIN).
/// </summary>
public static class RepoDomainClassifier
{
    private static readonly string[] WebProjectMarkers =
    [
        "Microsoft.NET.Sdk.Web",
        "Microsoft.AspNetCore.App",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Routing",
    ];

    private static readonly string[] WebDiffMarkers =
    [
        "[HttpPost]",
        "[HttpGet]",
        "[HttpPut]",
        "[HttpDelete]",
        "[ApiController]",
        "WebApplication.CreateBuilder",
        "AddControllers(",
        "MapControllers(",
        "AddEndpointsApiExplorer",
        "UseAuthentication(",
        "UseAuthorization(",
    ];

    /// <summary>Classifies the repo using config override, on-disk csproj scan, and diff heuristics.</summary>
    public static RepoDomainProfile Classify(string? repoPath, DiffContext diff, RepoDomainConfig config)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(config);

        if (TryParseOverride(config.Profile, out var overrideKind))
        {
            return new RepoDomainProfile
            {
                Kind = overrideKind,
                Reason = $"Configured domain profile override: {config.Profile}",
            };
        }

        var (webProjects, libraryProjects) = ScanProjects(repoPath);
        if (webProjects > 0)
        {
            return new RepoDomainProfile
            {
                Kind = RepoDomainKind.WebApplication,
                Reason = $"Detected {webProjects} ASP.NET/web project(s) under repository root.",
            };
        }

        if (libraryProjects > 0 && !DiffContainsWebSignals(diff))
        {
            return new RepoDomainProfile
            {
                Kind = RepoDomainKind.ClassLibrary,
                Reason = $"Detected {libraryProjects} non-web .csproj file(s) with no ASP.NET markers in diff.",
            };
        }

        if (DiffContainsWebSignals(diff))
        {
            return new RepoDomainProfile
            {
                Kind = RepoDomainKind.WebApplication,
                Reason = "Diff contains ASP.NET controller or host composition markers.",
            };
        }

        return new RepoDomainProfile
        {
            Kind = RepoDomainKind.Unknown,
            Reason = "Insufficient signals to classify repository domain.",
        };
    }

    private static bool TryParseOverride(string profile, out RepoDomainKind kind)
    {
        if (profile.Equals("library", StringComparison.OrdinalIgnoreCase))
        {
            kind = RepoDomainKind.ClassLibrary;
            return true;
        }

        if (profile.Equals("web", StringComparison.OrdinalIgnoreCase))
        {
            kind = RepoDomainKind.WebApplication;
            return true;
        }

        kind = RepoDomainKind.Unknown;
        return false;
    }

    private static (int WebProjects, int LibraryProjects) ScanProjects(string? repoPath)
    {
        if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath))
            return (0, 0);

        var web = 0;
        var library = 0;

        try
        {
            foreach (var csproj in Directory.EnumerateFiles(repoPath, "*.csproj", SearchOption.AllDirectories))
            {
                if (IsUnderExcludedFolder(csproj))
                    continue;

                string xml;
                try
                {
                    using var stream = File.OpenRead(csproj);
                    using var reader = new StreamReader(stream);
                    xml = reader.ReadToEnd();
                }
                catch (IOException) { continue; }

                if (WebProjectMarkers.Any(m => xml.Contains(m, StringComparison.OrdinalIgnoreCase)))
                {
                    web++;
                    continue;
                }

                if (xml.Contains("<Project", StringComparison.OrdinalIgnoreCase))
                    library++;
            }
        }
        catch (Exception)
        {
            // Best-effort classification only
        }

        return (web, library);
    }

    private static bool IsUnderExcludedFolder(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p =>
            p.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private static bool DiffContainsWebSignals(DiffContext diff) =>
        diff.Files.Any(f =>
            f.AddedLines.Any(l =>
                WebDiffMarkers.Any(m => l.Content.Contains(m, StringComparison.Ordinal))));
}
