using System;
using System.Collections.Generic;
using System.Linq;

namespace Dorado.Domain.Models;

public enum MixNodeType
{
    Artist,
    Album,
    Track
}

public class MixNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string? ImageUri { get; set; }
    public MixNodeType NodeType { get; set; } = MixNodeType.Artist;
    public double OrbitAngleDegrees { get; set; }
    public double OrbitRadius { get; set; }
    public double RelativeX { get; set; }
    public double RelativeY { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsCenterSeed { get; set; }
    public Guid? EntityId { get; set; }
}

public class MixConstellation
{
    public MixNode CenterSeed { get; set; } = new();
    public List<MixNode> Satellites { get; set; } = new();
}

public class MixStackEntry
{
    public MixNode SeedNode { get; }
    public List<MixNode> Satellites { get; }

    public MixStackEntry(MixNode seedNode, IEnumerable<MixNode> satellites)
    {
        SeedNode = seedNode;
        Satellites = satellites.ToList();
    }
}
