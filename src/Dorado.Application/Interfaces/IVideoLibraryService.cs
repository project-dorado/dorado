using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

public interface IVideoLibraryService
{
    Task<IReadOnlyList<Video>> GetAllVideosAsync();

    Task ScanDirectoryAsync(string directoryPath, IProgress<double>? progress = null);

    Task MarkPlayedAsync(Guid videoId);
}
