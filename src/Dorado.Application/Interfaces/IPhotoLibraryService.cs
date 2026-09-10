using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

public interface IPhotoLibraryService
{
    Task<IReadOnlyList<Photo>> GetAllPhotosAsync();

    Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null);

    /// <summary>Distinct folder paths currently present in the photo library.</summary>
    Task<IReadOnlyList<string>> GetFoldersAsync();
}
