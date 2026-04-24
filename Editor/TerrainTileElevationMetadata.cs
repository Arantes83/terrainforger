using System;
using System.IO;
using UnityEngine;

[Serializable]
public class TerrainTileElevationMetadata
{
    public string rawFileName = string.Empty;
    public int row;
    public int col;
    public float minElevation;
    public float maxElevation;
    public float tileSizeX;
    public float tileSizeZ;
    public float positionOffsetX;
    public float positionOffsetZ;
    public double north;
    public double south;
    public double west;
    public double east;
}

public static class TerrainTileElevationMetadataUtility
{
    private const string MetadataExtension = ".terrainforger.json";

    public static string GetMetadataPath(string rawPath)
    {
        return $"{rawPath}{MetadataExtension}";
    }

    public static void Write(string rawPath, TerrainTileElevationMetadata metadata)
    {
        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var metadataPath = GetMetadataPath(rawPath);
        var json = JsonUtility.ToJson(metadata, true);
        File.WriteAllText(metadataPath, json);
    }

    public static TerrainTileElevationMetadata TryRead(string rawPath)
    {
        var metadataPath = GetMetadataPath(rawPath);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var json = File.ReadAllText(metadataPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonUtility.FromJson<TerrainTileElevationMetadata>(json);
    }
}
