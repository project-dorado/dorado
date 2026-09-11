using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dorado.Application.Interfaces;
using Dorado.Domain.Enums;
using Dorado.Domain.Models;

namespace Dorado.Application.Services;

public class MixviewCoordinator : IMixviewService
{
    private readonly IMediaLibraryService _libraryService;
    private readonly IArtistRelationshipService? _artistRelationships;

    public MixviewCoordinator(IMediaLibraryService libraryService, IArtistRelationshipService? artistRelationships = null)
    {
        _libraryService = libraryService;
        _artistRelationships = artistRelationships;
    }

    public async Task<MixConstellation> GenerateConstellationAsync(
        string seedName, 
        MixNodeType seedType, 
        Guid? seedId = null, 
        CancellationToken cancellationToken = default)
    {
        var constellation = new MixConstellation();
        constellation.CenterSeed = new MixNode
        {
            Id = seedId ?? Guid.NewGuid(),
            Title = string.IsNullOrWhiteSpace(seedName) ? "Artist" : seedName,
            Subtitle = seedType switch
            {
                MixNodeType.Artist => "SEED ARTIST",
                MixNodeType.Album => "SEED ALBUM",
                MixNodeType.Track => "SEED TRACK",
                _ => "SEED"
            },
            NodeType = seedType,
            IsCenterSeed = true,
            OrbitAngleDegrees = 0,
            OrbitRadius = 0,
            X = 0,
            Y = 0,
            EntityId = seedId
        };

        var allTracks = await _libraryService.GetAllTracksAsync();
        var allAlbums = await _libraryService.GetAllAlbumsAsync();
        var allArtists = await _libraryService.GetAllArtistsAsync();

        var satellites = new List<MixNode>();

        // 1. Find directly matching artist tracks and albums
        var seedArtist = allArtists.FirstOrDefault(a => a.Name.Equals(seedName, StringComparison.OrdinalIgnoreCase));
        var seedTracks = allTracks.Where(t => t.ArtistName.Equals(seedName, StringComparison.OrdinalIgnoreCase)).ToList();
        var seedGenre = seedTracks.FirstOrDefault(t => !string.IsNullOrEmpty(t.Genre))?.Genre;

        // Add related albums
        var artistAlbums = allAlbums.Where(a => a.ArtistName.Equals(seedName, StringComparison.OrdinalIgnoreCase)).Take(3).ToList();
        foreach (var alb in artistAlbums)
        {
            satellites.Add(new MixNode
            {
                Id = alb.Id,
                Title = alb.Title,
                Subtitle = $"ALBUM • {alb.Year}",
                NodeType = MixNodeType.Album,
                EntityId = alb.Id
            });
        }

        // Add related tracks
        foreach (var trk in seedTracks.Take(3))
        {
            if (satellites.All(s => s.Id != trk.Id))
            {
                satellites.Add(new MixNode
                {
                    Id = trk.Id,
                    Title = trk.Title,
                    Subtitle = $"TRACK • {trk.Genre}",
                    NodeType = MixNodeType.Track,
                    EntityId = trk.Id
                });
            }
        }

        // Genre-affinity weighted related artists — the local substitute for Zune's
        // marketplace-backed MixQuery. Artists sharing the seed's genres and holding
        // favorited tracks rank higher in the constellation.
        var seedGenreSet = seedTracks
            .Select(t => t.Genre)
            .Where(g => !string.IsNullOrEmpty(g))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var relatedArtistScores = allArtists
            .Where(a => !a.Name.Equals(seedName, StringComparison.OrdinalIgnoreCase))
            .Select(a =>
            {
                var artistTracks = allTracks
                    .Where(t => t.ArtistName.Equals(a.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var sharedGenres = artistTracks.Count(t => !string.IsNullOrEmpty(t.Genre) && seedGenreSet.Contains(t.Genre));
                var favorites = artistTracks.Count(t => t.Rating == HeartRating.Favorite);
                return (Artist: a, Score: (sharedGenres * 3) + (favorites * 2) + Math.Min(artistTracks.Count, 10));
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(4)
            .ToList();

        foreach (var (artist, _) in relatedArtistScores)
        {
            satellites.Add(new MixNode
            {
                Id = artist.Id,
                Title = artist.Name,
                Subtitle = seedGenreSet.Count == 0 ? "RELATED ARTIST" : "GENRE AFFINITY",
                NodeType = MixNodeType.Artist,
                EntityId = artist.Id
            });
        }

        // External related-artist enrichment (MusicBrainz): surface artists the
        // local genre heuristic missed, but only when they exist in the library
        // (so the node is clickable). Best-effort: failures leave the local
        // constellation untouched.
        if (_artistRelationships is not null)
        {
            var present = satellites
                .Where(s => s.NodeType == MixNodeType.Artist)
                .Select(s => s.Title)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            present.Add(seedName);

            try
            {
                var relatedNames = await _artistRelationships
                    .GetRelatedArtistsAsync(seedName, limit: 8, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var name in relatedNames)
                {
                    if (!present.Add(name))
                    {
                        continue;
                    }

                    var local = allArtists.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (local is null)
                    {
                        continue;
                    }

                    satellites.Add(new MixNode
                    {
                        Id = local.Id,
                        Title = local.Name,
                        Subtitle = "RELATED ARTIST",
                        NodeType = MixNodeType.Artist,
                        EntityId = local.Id
                    });

                    if (satellites.Count(s => s.NodeType == MixNodeType.Artist) >= 8)
                    {
                        break;
                    }
                }
            }
            catch
            {
                // External enrichment is optional.
            }
        }

        // Calculate organic orbital coordinates
        int count = satellites.Count;
        for (int i = 0; i < count; i++)
        {
            double angleDeg = (360.0 / count) * i;
            // Alternating radii creates the iconic staggered Zune constellation depth
            double radius = (i % 2 == 0) ? 220.0 : 310.0;
            double angleRad = angleDeg * (Math.PI / 180.0);

            satellites[i].OrbitAngleDegrees = angleDeg;
            satellites[i].OrbitRadius = radius;
            satellites[i].RelativeX = Math.Round(Math.Cos(angleRad) * radius);
            satellites[i].RelativeY = Math.Round(Math.Sin(angleRad) * radius);
            satellites[i].X = satellites[i].RelativeX + 500;
            satellites[i].Y = satellites[i].RelativeY + 300;
        }

        constellation.Satellites = satellites;
        return constellation;
    }
}
