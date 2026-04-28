// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;

namespace GauntletCI.Core.Configuration;

/// <summary>
/// Loads .gauntletci.json from the repository root.
/// Returns a default config if the file doesn't exist or cannot be parsed.
/// </summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads <c>.gauntletci.json</c> from the repository root and deserializes it into a <see cref="GauntletConfig"/>.
    /// </summary>
    /// <param name="repoPath">Absolute or relative path to the repository root where .gauntletci.json should be found.</param>
    /// <returns>
    /// The deserialized configuration, or a default <see cref="GauntletConfig"/> if the file does not exist
    /// or cannot be parsed.
    /// </returns>
    public static GauntletConfig Load(string repoPath)
    {
        var path = Path.Combine(repoPath, ".gauntletci.json");
        if (!File.Exists(path)) return new GauntletConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GauntletConfig>(json, JsonOptions) ?? new GauntletConfig();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("[GauntletCI] Warning: could not parse .gauntletci.json: using defaults.");
            return new GauntletConfig();
        }
    }
}
