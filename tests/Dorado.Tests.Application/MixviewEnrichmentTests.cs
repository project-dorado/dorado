using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Models;
using Dorado.Infrastructure.External;
using Dorado.Tests.Application.TestFakes;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Covers the Mixview external related-artist enrichment (Phase 2 of the parity
/// leftovers): the MusicBrainz relationship lookup and the coordinator's merge
/// of in-library related artists.
/// </summary>
public sealed class MixviewEnrichmentTests
{
    [Fact]
    public async Task Coordinator_AddsInLibraryRelatedArtistsFromTheRelationshipService()
    {
        var seedId = Guid.NewGuid();
        var library = new FakeMediaLibraryService();
        library.Artists.Add(new Artist { Id = seedId, Name = "Seed Artist" });
        library.Artists.Add(new Artist { Id = Guid.NewGuid(), Name = "External Related" });
        library.Tracks.Add(new Track { Title = "Seeded", ArtistName = "Seed Artist", Genre = "Rock" });

        var coordinator = new MixviewCoordinator(library, new FakeRelationships("External Related", "Unknown Artist", "Seed Artist"));

        var constellation = await coordinator.GenerateConstellationAsync("Seed Artist", MixNodeType.Artist, seedId);

        var artistTitles = constellation.Satellites
            .Where(s => s.NodeType == MixNodeType.Artist)
            .Select(s => s.Title)
            .ToList();
        Assert.Contains("External Related", artistTitles);
        Assert.DoesNotContain("Unknown Artist", artistTitles); // not in the library
        Assert.DoesNotContain("Seed Artist", artistTitles);    // seed excluded
    }

    [Fact]
    public async Task Coordinator_WithoutRelationshipService_StaysLocal()
    {
        var seedId = Guid.NewGuid();
        var library = new FakeMediaLibraryService();
        library.Artists.Add(new Artist { Id = seedId, Name = "Seed Artist" });
        library.Artists.Add(new Artist { Id = Guid.NewGuid(), Name = "External Related" });

        var coordinator = new MixviewCoordinator(library);

        var constellation = await coordinator.GenerateConstellationAsync("Seed Artist", MixNodeType.Artist, seedId);

        Assert.DoesNotContain(constellation.Satellites, s => s.Title == "External Related");
    }

    [Fact]
    public async Task MusicBrainzClient_ParsesRelatedArtistsAndDeduplicates()
    {
        var handler = new FakeHttpMessageHandler();
        handler.MapJson(u => u.Contains("/ws/2/artist/?query="), "{\"artists\":[{\"id\":\"mbid-1\",\"score\":100}]}");
        handler.MapJson(
            u => u.Contains("/ws/2/artist/mbid-1"),
            "{\"relations\":[" +
            "{\"artist\":{\"name\":\"Collaborator A\"}}," +
            "{\"artist\":{\"name\":\"Collaborator B\"}}," +
            "{\"artist\":{\"name\":\"Collaborator A\"}}," +
            "{\"artist\":{\"name\":\"Seed Artist\"}}]}");

        var client = new MusicBrainzClient(new HttpClient(handler));

        var names = await client.LookupRelatedArtistsAsync("Seed Artist", limit: 8, CancellationToken.None);

        Assert.Equal(new[] { "Collaborator A", "Collaborator B" }, names);
    }

    private sealed class FakeRelationships(params string[] names) : IArtistRelationshipService
    {
        public Task<IReadOnlyList<string>> GetRelatedArtistsAsync(
            string artistName, int limit = 8, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(names.Take(limit).ToList());
    }
}
