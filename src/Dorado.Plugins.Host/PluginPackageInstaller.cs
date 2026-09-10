using System.IO.Compression;

namespace Dorado.Plugins.Host;

/// <summary>
/// Installs <c>.znp</c> (zip) plugin packages into the plugins directory. Extraction is
/// staged and validated so a malformed or zip-slip package cannot corrupt an install.
/// </summary>
public sealed class PluginPackageInstaller
{
    private readonly string _pluginsDirectory;

    public PluginPackageInstaller(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
        Directory.CreateDirectory(_pluginsDirectory);
    }

    public async Task<InstalledPlugin> InstallAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Plugin package not found.", packagePath);
        }

        if (!packagePath.EndsWith(".znp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Plugin packages must use the .znp extension.");
        }

        using var archive = ZipFile.OpenRead(packagePath);
        var manifestEntry = archive.GetEntry(PluginManifest.FileName)
            ?? archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + PluginManifest.FileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Package does not contain {PluginManifest.FileName}.");

        PluginManifest manifest;
        await using (var stream = manifestEntry.Open())
        using (var reader = new StreamReader(stream))
        {
            manifest = PluginManifest.Parse(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false));
        }

        var errors = manifest.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidDataException("Invalid plugin.json: " + string.Join("; ", errors));
        }

        var basePrefix = manifestEntry.FullName.Equals(PluginManifest.FileName, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : manifestEntry.FullName[..^PluginManifest.FileName.Length];

        var targetDirectory = Path.Combine(_pluginsDirectory, manifest.Id);
        var stagingDirectory = targetDirectory + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var stagingRoot = Path.GetFullPath(stagingDirectory);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/'))
                {
                    continue;
                }

                if (basePrefix.Length > 0 && !entry.FullName.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relative = basePrefix.Length > 0 ? entry.FullName[basePrefix.Length..] : entry.FullName;
                if (string.IsNullOrEmpty(relative))
                {
                    continue;
                }

                var destination = Path.GetFullPath(Path.Combine(stagingDirectory, relative));
                if (!destination.StartsWith(stagingRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Package entry escapes the install directory.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
            }

            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, recursive: true);
            }

            Directory.Move(stagingDirectory, targetDirectory);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }

        return new InstalledPlugin { Manifest = manifest, InstallDirectory = targetDirectory };
    }

    public void Uninstall(string pluginId)
    {
        if (!PluginManifest.IsSafeId(pluginId))
        {
            throw new InvalidDataException($"Unsafe plugin id: {pluginId}");
        }

        var directory = Path.Combine(_pluginsDirectory, pluginId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
