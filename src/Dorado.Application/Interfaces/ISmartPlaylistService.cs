using Dorado.Domain.Models;

namespace Dorado.Application.Interfaces;

public interface ISmartPlaylistService
{
    Task<IReadOnlyList<SmartPlaylist>> GetAllAsync();

    Task SaveAsync(SmartPlaylist playlist);

    Task DeleteAsync(Guid playlistId);

    /// <summary>
    /// Evaluates the rule set against the given library tracks, applies sorting and the track limit.
    /// </summary>
    IReadOnlyList<Track> Evaluate(SmartPlaylist playlist, IEnumerable<Track> libraryTracks);
}
