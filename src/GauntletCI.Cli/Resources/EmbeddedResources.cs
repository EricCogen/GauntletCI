// SPDX-License-Identifier: Elastic-2.0
using System.Reflection;

namespace GauntletCI.Cli.Resources;

public static class EmbeddedResources
{
    public static string ReadText(string resourceFileName)
    {
        var assembly = typeof(EmbeddedResources).Assembly;

        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith($".{resourceFileName}", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new FileNotFoundException($"Embedded resource not found: {resourceFileName}");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Unable to open embedded resource stream: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
