using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Dorado.Application.Interfaces;

namespace Dorado.UI.Services;

public class AvaloniaFolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(string title = "Select Music Collection Folder")
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.StorageProvider is { } storageProvider)
        {
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                return folders[0].TryGetLocalPath();
            }
        }
        return null;
    }

    public async Task<string?> PickFileAsync(string title = "Select File", string extension = "*.*")
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.StorageProvider is { } storageProvider)
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(extension.TrimStart('*').TrimStart('.').ToUpperInvariant())
                    {
                        Patterns = new[] { extension.StartsWith("*") ? extension : $"*{extension}" }
                    }
                }
            });

            if (files.Count > 0)
            {
                return files[0].TryGetLocalPath();
            }
        }
        return null;
    }
}
