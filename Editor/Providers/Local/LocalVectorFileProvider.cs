using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class LocalVectorFileProvider : IVectorDataProvider
{
    public string DisplayName
    {
        get { return "Local Vector File"; }
    }

    public VectorDataset Import(TerrainForgerVectorImportSettings settings, TerrainForgerVectorImportContext context)
    {
        if (string.IsNullOrWhiteSpace(settings.localVectorFilePath))
        {
            throw new InvalidOperationException("Choose a local vector file first.");
        }

        var inputPath = TerrainForgerVectorExternalTools.ResolvePath(settings.localVectorFilePath);
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Local vector file not found.", inputPath);
        }

        var extension = Path.GetExtension(inputPath);
        if (!IsSupportedExtension(extension))
        {
            throw new InvalidOperationException("Supported local vector formats are .shp, .geojson, .json, .gpkg and .kml.");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "TerrainForgerVector", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var outputGeoJsonPath = Path.Combine(tempRoot, "local_vector.geojson");

        try
        {
            context.AddLog("Converting local vector file with GDAL/OGR.");
            GdalOgrVectorConverter.ConvertToGeoJson(
                inputPath,
                outputGeoJsonPath,
                context.Bounds,
                settings.localLayerName);

            var geoJson = File.ReadAllText(outputGeoJsonPath);
            var dataset = TerrainForgerGeoJsonUtility.ReadDataset(
                geoJson,
                Path.GetFileName(inputPath),
                settings.localLayerMode);
            dataset.Bounds = context.Bounds;
            context.AddLog("Local vector file normalized.");
            return dataset;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TerrainForger Vector] Failed to delete temp folder: " + ex.Message);
            }
        }
    }

    private static bool IsSupportedExtension(string extension)
    {
        return string.Equals(extension, ".shp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".geojson", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".gpkg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".kml", StringComparison.OrdinalIgnoreCase);
    }
}

public static class GdalOgrVectorConverter
{
    public static void ConvertToGeoJson(string inputPath, string outputGeoJsonPath, GeoBounds bounds, string layerName)
    {
        if (!bounds.IsValid)
        {
            throw new InvalidOperationException("Vector import bounds are invalid.");
        }

        var executable = TerrainForgerVectorExternalTools.ResolveQgisExecutable("ogr2ogr.exe");
        if (File.Exists(outputGeoJsonPath))
        {
            File.Delete(outputGeoJsonPath);
        }

        var args = new List<string>
        {
            "-f",
            "GeoJSON",
            "-overwrite",
            "-t_srs",
            "EPSG:4326",
            "-clipsrc",
            TerrainForgerVectorExternalTools.FormatDouble(bounds.west),
            TerrainForgerVectorExternalTools.FormatDouble(bounds.south),
            TerrainForgerVectorExternalTools.FormatDouble(bounds.east),
            TerrainForgerVectorExternalTools.FormatDouble(bounds.north),
            "-lco",
            "RFC7946=YES",
            TerrainForgerVectorExternalTools.Quote(outputGeoJsonPath),
            TerrainForgerVectorExternalTools.Quote(inputPath)
        };

        if (!string.IsNullOrWhiteSpace(layerName))
        {
            args.Add(TerrainForgerVectorExternalTools.Quote(layerName));
        }

        TerrainForgerVectorExternalTools.RunProcess(
            executable,
            string.Join(" ", args.ToArray()),
            "QGIS ogr2ogr");
    }
}
