using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace Dorado.UI.Converters;

/// <summary>
/// Renders the photo folder tree entries: the sentinel shows "All Photos",
/// other entries show the folder's display name.
/// </summary>
public class PhotoFolderPathConverter : IValueConverter
{
    public static readonly PhotoFolderPathConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path)
        {
            return string.Empty;
        }

        return path == Dorado.UI.ViewModels.PhotoLibraryViewModel.AllFoldersEntry
            ? "All Photos"
            : Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
