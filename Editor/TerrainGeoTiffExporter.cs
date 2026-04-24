using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class TerrainGeoTiffExporter
{
    private const string TempFolderName = "TerrainGeoTiffExport";

    public static void ExportToRawTiles(TerrainTileImportConfig config)
    {
        ExportTiles(config, exportDem: true, exportSatellite: false);
    }

    public static void ExportSatelliteTiles(TerrainTileImportConfig config)
    {
        ExportTiles(config, exportDem: false, exportSatellite: true);
    }

    public static void ExportTerrainPackage(TerrainTileImportConfig config)
    {
        ExportTiles(config, exportDem: true, exportSatellite: true);
    }

    private static void ExportTiles(TerrainTileImportConfig config, bool exportDem, bool exportSatellite)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (!exportDem && !exportSatellite)
        {
            throw new InvalidOperationException("Choose at least one export target.");
        }

        ValidateCommonConfig(config);
        if (exportDem)
        {
            ValidateDemConfig(config);
        }

        if (exportSatellite)
        {
            ValidateSatelliteConfig(config);
        }

        var demGeoTiffPath = ResolveOptionalPath(config.geoTiffPath);
        var satelliteGeoTiffPath = ResolveOptionalPath(config.satelliteGeoTiffPath);
        var rawOutputFolder = ResolvePath(config.inputFolder);
        var satelliteOutputFolder = ResolvePath(config.satelliteOutputFolder);

        if (exportDem)
        {
            Directory.CreateDirectory(rawOutputFolder);
        }

        if (exportSatellite)
        {
            Directory.CreateDirectory(satelliteOutputFolder);
        }

        var bounds = GetBounds(config, exportDem, exportSatellite, demGeoTiffPath, satelliteGeoTiffPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), TempFolderName, Guid.NewGuid().ToString("N"));
        var exportedDemMetadata = new List<TerrainTileElevationMetadata>();
        Directory.CreateDirectory(tempRoot);

        try
        {
            var totalSteps = config.rows * config.cols;
            for (var row = 0; row < config.rows; row++)
            {
                for (var col = 0; col < config.cols; col++)
                {
                    var tileIndex = (row * config.cols) + col;
                    var progress = tileIndex / (float)Math.Max(1, totalSteps);
                    var tileLabel = TerrainTileNaming.GetTileLabel(row, col);
                    var title = exportDem && exportSatellite
                        ? "Exporting DEM and SAT tiles"
                        : exportDem ? "Exporting DEM tiles" : "Exporting SAT tiles";
                    EditorUtility.DisplayProgressBar(title, $"Exporting tile {tileLabel}", progress);

                    var tileBounds = GetTileBounds(bounds, config.rows, config.cols, row, col);

                    if (exportDem)
                    {
                        var metadata = ExportDemTile(config, demGeoTiffPath, rawOutputFolder, tempRoot, bounds, row, col, tileBounds);
                        exportedDemMetadata.Add(metadata);
                    }

                    if (exportSatellite)
                    {
                        ExportSatelliteTile(config, satelliteGeoTiffPath, satelliteOutputFolder, row, col, tileBounds);
                    }
                }
            }

            if (config.writeExportManifest)
            {
                var manifestFolder = exportDem ? rawOutputFolder : satelliteOutputFolder;
                WriteManifest(config, manifestFolder, bounds, exportDem, exportSatellite, exportedDemMetadata);
            }

            if (exportDem)
            {
                AlignImportSettingsWithExport(config, exportedDemMetadata);
            }

            AssetDatabase.Refresh();
            Debug.Log($"[TerrainForger Export] Exported {config.rows * config.cols} tile(s). DEM: {exportDem}, SAT: {exportSatellite}.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private static void AlignImportSettingsWithExport(TerrainTileImportConfig config, List<TerrainTileElevationMetadata> exportedDemMetadata)
    {
        config.inputIsLittleEndian = true;
        config.flipHorizontally = false;
        config.flipVertically = true;

        if (exportedDemMetadata != null && exportedDemMetadata.Count > 0)
        {
            config.minElevation = exportedDemMetadata.Min(metadata => metadata.minElevation);
            config.maxElevation = exportedDemMetadata.Max(metadata => metadata.maxElevation);
        }
    }

    private static TerrainTileElevationMetadata ExportDemTile(
        TerrainTileImportConfig config,
        string demGeoTiffPath,
        string rawOutputFolder,
        string tempRoot,
        TileBounds globalBounds,
        int row,
        int col,
        TileBounds tileBounds)
    {
        var tileLabel = TerrainTileNaming.GetTileLabel(row, col);
        var tempBinPath = Path.Combine(tempRoot, $"{tileLabel}_float32.bin");

        RunGdalWarp(
            config.qgisInstallFolder,
            demGeoTiffPath,
            tempBinPath,
            tileBounds.west,
            tileBounds.south,
            tileBounds.east,
            tileBounds.north,
            config.heightmapResolution,
            config.heightmapResolution,
            "Float32",
            "ENVI");

        var raster = EnviFloatRaster.Read(tempBinPath);
        if (raster.Width != config.heightmapResolution || raster.Height != config.heightmapResolution)
        {
            throw new InvalidOperationException(
                $"Unexpected GDAL output size for tile {tileLabel}. Expected {config.heightmapResolution}x{config.heightmapResolution}, got {raster.Width}x{raster.Height}.");
        }

        if (config.exportUseGshhgMask)
        {
            ApplyGshhgMask(config, raster, tempRoot, tileLabel, tileBounds);
        }

        var rawFileName = TerrainTileNaming.ResolvePattern(config.filePattern, row, col);
        var outputRawPath = Path.Combine(rawOutputFolder, rawFileName);
        var metadata = WriteRaw16Tile(config, raster, outputRawPath);
        metadata.rawFileName = rawFileName;
        metadata.row = row;
        metadata.col = col;
        ApplyTileSpatialMetadata(metadata, globalBounds, tileBounds);
        TerrainTileElevationMetadataUtility.Write(outputRawPath, metadata);
        return metadata;
    }

    private static void ExportSatelliteTile(
        TerrainTileImportConfig config,
        string satelliteGeoTiffPath,
        string satelliteOutputFolder,
        int row,
        int col,
        TileBounds tileBounds)
    {
        var pngFileName = GetSatelliteTileFileName(config.filePattern, row, col);
        var outputPngPath = Path.Combine(satelliteOutputFolder, pngFileName);

        RunGdalWarp(
            config.qgisInstallFolder,
            satelliteGeoTiffPath,
            outputPngPath,
            tileBounds.west,
            tileBounds.south,
            tileBounds.east,
            tileBounds.north,
            config.satelliteTileResolution,
            config.satelliteTileResolution,
            dataType: null,
            format: "PNG");
    }

    private static void ApplyGshhgMask(
        TerrainTileImportConfig config,
        EnviFloatRaster raster,
        string tempRoot,
        string tileLabel,
        TileBounds tileBounds)
    {
        var tempMaskPath = Path.Combine(tempRoot, $"{tileLabel}_landmask.bin");
        RunGdalRasterize(
            config.qgisInstallFolder,
            ResolvePath(config.gshhgVectorPath),
            tempMaskPath,
            tileBounds.west,
            tileBounds.south,
            tileBounds.east,
            tileBounds.north,
            config.heightmapResolution,
            config.heightmapResolution,
            burnValue: 1);

        var landMask = EnviByteRaster.Read(tempMaskPath);
        if (landMask.Width != raster.Width || landMask.Height != raster.Height)
        {
            throw new InvalidOperationException(
                $"Unexpected GSHHG mask size for tile {tileLabel}. Expected {raster.Width}x{raster.Height}, got {landMask.Width}x{landMask.Height}.");
        }

        raster.RemapValuesOutsideLandMask(landMask.Values, config.exportWaterMaskElevation);
    }

    private static void RunGdalWarp(
        string qgisInstallFolder,
        string inputGeoTiffPath,
        string outputPath,
        double west,
        double south,
        double east,
        double north,
        int width,
        int height,
        string dataType,
        string format)
    {
        var executable = ResolveQgisExecutable(qgisInstallFolder, "gdalwarp.exe");
        var args = new List<string>
        {
            "-overwrite",
            "-r bilinear",
            "-t_srs EPSG:4326",
            "-te_srs EPSG:4326",
            "-te",
            FormatDouble(west),
            FormatDouble(south),
            FormatDouble(east),
            FormatDouble(north),
            "-ts",
            width.ToString(CultureInfo.InvariantCulture),
            height.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(dataType))
        {
            args.Add("-ot");
            args.Add(dataType);
        }

        args.Add("-of");
        args.Add(format);
        args.Add(Quote(inputGeoTiffPath));
        args.Add(Quote(outputPath));

        RunProcess(executable, string.Join(" ", args), "QGIS gdalwarp");
    }

    private static void RunGdalRasterize(
        string qgisInstallFolder,
        string inputVectorPath,
        string outputPath,
        double west,
        double south,
        double east,
        double north,
        int width,
        int height,
        byte burnValue)
    {
        var executable = ResolveQgisExecutable(qgisInstallFolder, "gdal_rasterize.exe");
        var args = new List<string>
        {
            "-burn",
            burnValue.ToString(CultureInfo.InvariantCulture),
            "-init",
            "0",
            "-at",
            "-ot",
            "Byte",
            "-of",
            "ENVI",
            "-a_srs",
            "EPSG:4326",
            "-te",
            FormatDouble(west),
            FormatDouble(south),
            FormatDouble(east),
            FormatDouble(north),
            "-ts",
            width.ToString(CultureInfo.InvariantCulture),
            height.ToString(CultureInfo.InvariantCulture),
            Quote(inputVectorPath),
            Quote(outputPath)
        };

        RunProcess(executable, string.Join(" ", args), "QGIS gdal_rasterize");
    }

    private static TerrainTileElevationMetadata WriteRaw16Tile(TerrainTileImportConfig config, EnviFloatRaster raster, string outputRawPath)
    {
        var processedMin = float.PositiveInfinity;
        var processedMax = float.NegativeInfinity;

        for (var y = 0; y < raster.Height; y++)
        {
            for (var x = 0; x < raster.Width; x++)
            {
                var value = NormalizeRasterSample(raster.GetValue(y, x), config.exportUseGshhgMask ? config.exportWaterMaskElevation : 0f);
                processedMin = Mathf.Min(processedMin, value);
                processedMax = Mathf.Max(processedMax, value);
            }
        }

        if (float.IsInfinity(processedMin) || float.IsInfinity(processedMax))
        {
            processedMin = 0f;
            processedMax = 0f;
        }

        var exportMin = processedMin;
        var exportMax = processedMax;
        var isFlatTile = Mathf.Approximately(exportMax, exportMin);

        if (!isFlatTile && exportMax <= exportMin)
        {
            throw new InvalidOperationException("Effective export max elevation must be greater than effective export min elevation.");
        }

        using (var stream = File.Open(outputRawPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(stream))
        {
            for (var y = 0; y < raster.Height; y++)
            {
                for (var x = 0; x < raster.Width; x++)
                {
                    var value = NormalizeRasterSample(raster.GetValue(y, x), config.exportUseGshhgMask ? config.exportWaterMaskElevation : 0f);
                    var normalized = isFlatTile ? 0f : Mathf.InverseLerp(exportMin, exportMax, value);
                    var raw = (ushort)Mathf.Clamp(Mathf.RoundToInt(normalized * 65535f), 0, 65535);
                    writer.Write(raw);
                }
            }
        }

        return new TerrainTileElevationMetadata
        {
            minElevation = exportMin,
            maxElevation = exportMax
        };
    }

    private static float NormalizeRasterSample(float value, float fallbackValue)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return fallbackValue;
        }

        return value;
    }

    private static void ApplyTileSpatialMetadata(
        TerrainTileElevationMetadata metadata,
        TileBounds globalBounds,
        TileBounds tileBounds)
    {
        var centerLatitude = (globalBounds.north + globalBounds.south) * 0.5d;
        var xMin = LongitudeToMeters(tileBounds.west, globalBounds.west, centerLatitude);
        var xMax = LongitudeToMeters(tileBounds.east, globalBounds.west, centerLatitude);
        var zMin = LatitudeToMeters(tileBounds.south, globalBounds.south);
        var zMax = LatitudeToMeters(tileBounds.north, globalBounds.south);

        metadata.tileSizeX = (float)Math.Max(0.01d, xMax - xMin);
        metadata.tileSizeZ = (float)Math.Max(0.01d, zMax - zMin);
        metadata.positionOffsetX = (float)xMin;
        metadata.positionOffsetZ = (float)zMin;
        metadata.north = tileBounds.north;
        metadata.south = tileBounds.south;
        metadata.west = tileBounds.west;
        metadata.east = tileBounds.east;
    }

    private static void WriteManifest(
        TerrainTileImportConfig config,
        string outputFolder,
        TileBounds bounds,
        bool exportDem,
        bool exportSatellite,
        List<TerrainTileElevationMetadata> exportedDemMetadata)
    {
        var manifestPath = Path.Combine(outputFolder, "terrain-export-manifest.txt");
        var builder = new StringBuilder();
        builder.AppendLine("Terrain Tile Export Manifest");
        builder.AppendLine($"DEM GeoTIFF: {ResolveOptionalPath(config.geoTiffPath)}");
        builder.AppendLine($"Satellite GeoTIFF: {ResolveOptionalPath(config.satelliteGeoTiffPath)}");
        builder.AppendLine($"QGIS Install Folder: {config.qgisInstallFolder}");
        builder.AppendLine($"Rows: {config.rows}");
        builder.AppendLine($"Cols: {config.cols}");
        builder.AppendLine($"DEM Heightmap Resolution: {config.heightmapResolution}");
        builder.AppendLine($"Satellite Tile Resolution: {config.satelliteTileResolution}");
        builder.AppendLine($"North: {FormatDouble(bounds.north)}");
        builder.AppendLine($"South: {FormatDouble(bounds.south)}");
        builder.AppendLine($"West: {FormatDouble(bounds.west)}");
        builder.AppendLine($"East: {FormatDouble(bounds.east)}");
        builder.AppendLine($"File Pattern: {config.filePattern}");
        builder.AppendLine($"Export DEM: {exportDem}");
        builder.AppendLine($"Export Satellite: {exportSatellite}");
        builder.AppendLine($"Clamp Enabled: {config.exportClampElevation}");
        builder.AppendLine($"Use GSHHG Land Mask: {config.exportUseGshhgMask}");
        builder.AppendLine($"GSHHG Vector Path: {ResolveOptionalPath(config.gshhgVectorPath)}");
        builder.AppendLine($"Water Mask Elevation: {config.exportWaterMaskElevation}");
        builder.AppendLine($"Min Elevation: {config.minElevation}");
        builder.AppendLine($"Max Elevation: {config.maxElevation}");
        builder.AppendLine("Tiles:");

        for (var row = 0; row < config.rows; row++)
        {
            for (var col = 0; col < config.cols; col++)
            {
                var rawName = TerrainTileNaming.ResolvePattern(config.filePattern, row, col);
                var pngName = GetSatelliteTileFileName(config.filePattern, row, col);
                var tileMetadata = exportedDemMetadata?.FirstOrDefault(metadata =>
                    metadata.row == row && metadata.col == col);
                if (exportDem)
                {
                    if (tileMetadata != null)
                    {
                        builder.AppendLine(
                            $"RAW: {rawName} | Min: {tileMetadata.minElevation.ToString(CultureInfo.InvariantCulture)} | Max: {tileMetadata.maxElevation.ToString(CultureInfo.InvariantCulture)}");
                    }
                    else
                    {
                        builder.AppendLine($"RAW: {rawName}");
                    }
                }

                if (exportSatellite)
                {
                    builder.AppendLine($"PNG: {pngName}");
                }
            }
        }

        File.WriteAllText(manifestPath, builder.ToString());
    }

    private static void ValidateCommonConfig(TerrainTileImportConfig config)
    {
        if (config.rows <= 0 || config.cols <= 0)
        {
            throw new InvalidOperationException("Rows and columns must be greater than zero.");
        }

        if (config.heightmapResolution != 513 &&
            config.heightmapResolution != 1025 &&
            config.heightmapResolution != 2049 &&
            config.heightmapResolution != 4097)
        {
            throw new InvalidOperationException("GeoTIFF export supports only 513, 1025, 2049 and 4097 output resolutions for DEM RAW tiles.");
        }

        if (string.IsNullOrWhiteSpace(config.qgisInstallFolder))
        {
            throw new InvalidOperationException(
                "QGIS is required for GeoTIFF export. Set 'QGIS Install Folder' to the root folder of your QGIS installation.");
        }

        if (!Directory.Exists(ResolvePath(config.qgisInstallFolder)))
        {
            throw new DirectoryNotFoundException($"QGIS install folder not found: {ResolvePath(config.qgisInstallFolder)}");
        }

    }

    private static void ValidateDemConfig(TerrainTileImportConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.geoTiffPath))
        {
            throw new InvalidOperationException("DEM GeoTIFF path is required.");
        }

        var geoTiffPath = ResolvePath(config.geoTiffPath);
        if (!File.Exists(geoTiffPath))
        {
            throw new FileNotFoundException("DEM GeoTIFF file not found.", geoTiffPath);
        }

        if (string.IsNullOrWhiteSpace(config.inputFolder))
        {
            throw new InvalidOperationException("RAW output folder is required.");
        }

        if (config.exportUseGshhgMask)
        {
            if (string.IsNullOrWhiteSpace(config.gshhgVectorPath))
            {
                throw new InvalidOperationException("GSHHG vector path is required when 'Use GSHHG Land Mask' is enabled.");
            }

            var gshhgVectorPath = ResolvePath(config.gshhgVectorPath);
            if (!File.Exists(gshhgVectorPath))
            {
                throw new FileNotFoundException("GSHHG vector file not found.", gshhgVectorPath);
            }
        }

    }

    private static void ValidateSatelliteConfig(TerrainTileImportConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.satelliteGeoTiffPath))
        {
            throw new InvalidOperationException("Satellite GeoTIFF path is required.");
        }

        var satelliteGeoTiffPath = ResolvePath(config.satelliteGeoTiffPath);
        if (!File.Exists(satelliteGeoTiffPath))
        {
            throw new FileNotFoundException("Satellite GeoTIFF file not found.", satelliteGeoTiffPath);
        }

        if (string.IsNullOrWhiteSpace(config.satelliteOutputFolder))
        {
            throw new InvalidOperationException("Satellite PNG output folder is required.");
        }

        if (config.satelliteTileResolution <= 0)
        {
            throw new InvalidOperationException("Satellite tile resolution must be greater than zero.");
        }
    }

    private static TileBounds GetBounds(
        TerrainTileImportConfig config,
        bool exportDem,
        bool exportSatellite,
        string demGeoTiffPath,
        string satelliteGeoTiffPath)
    {
        var primarySourcePath = exportDem || string.IsNullOrWhiteSpace(satelliteGeoTiffPath)
            ? demGeoTiffPath
            : satelliteGeoTiffPath;
        var bounds = ReadBoundsFromRaster(config.qgisInstallFolder, primarySourcePath);

        if (exportDem && exportSatellite)
        {
            var satelliteBounds = ReadBoundsFromRaster(config.qgisInstallFolder, satelliteGeoTiffPath);
            ValidateBoundsAlignment(bounds, satelliteBounds, demGeoTiffPath, satelliteGeoTiffPath);
        }

        return bounds;
    }

    private static TileBounds GetTileBounds(TileBounds bounds, int rows, int cols, int row, int col)
    {
        return new TileBounds(
            Lerp(bounds.north, bounds.south, row / (double)rows),
            Lerp(bounds.north, bounds.south, (row + 1) / (double)rows),
            Lerp(bounds.west, bounds.east, col / (double)cols),
            Lerp(bounds.west, bounds.east, (col + 1) / (double)cols));
    }

    private static string GetSatelliteTileFileName(string pattern, int row, int col)
    {
        var rawName = TerrainTileNaming.ResolvePattern(pattern, row, col);
        return $"{Path.GetFileNameWithoutExtension(rawName)}.png";
    }

    private static TileBounds ReadBoundsFromRaster(string qgisInstallFolder, string rasterPath)
    {
        if (string.IsNullOrWhiteSpace(rasterPath))
        {
            throw new InvalidOperationException("A raster source is required to derive tile bounds.");
        }

        var executable = ResolveQgisExecutable(qgisInstallFolder, "gdalinfo.exe");
        var output = RunProcess(executable, $"-json {Quote(rasterPath)}", "QGIS gdalinfo");
        var bounds = ParseWgs84Bounds(output);

        if (bounds.north <= bounds.south)
        {
            throw new InvalidOperationException($"Invalid bounds detected in raster: {rasterPath}");
        }

        if (bounds.east <= bounds.west)
        {
            throw new InvalidOperationException($"Invalid bounds detected in raster: {rasterPath}");
        }

        return bounds;
    }

    private static void ValidateBoundsAlignment(TileBounds demBounds, TileBounds satelliteBounds, string demPath, string satellitePath)
    {
        const double tolerance = 0.000001d;
        if (Math.Abs(demBounds.north - satelliteBounds.north) > tolerance ||
            Math.Abs(demBounds.south - satelliteBounds.south) > tolerance ||
            Math.Abs(demBounds.west - satelliteBounds.west) > tolerance ||
            Math.Abs(demBounds.east - satelliteBounds.east) > tolerance)
        {
            throw new InvalidOperationException(
                $"DEM and satellite bounds do not match closely enough for a shared tile export.\nDEM: {demPath}\nSAT: {satellitePath}");
        }
    }

    private static string ResolveQgisExecutable(string qgisInstallFolder, string executableName)
    {
        var resolvedFolder = ResolvePath(qgisInstallFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(resolvedFolder))
        {
            throw new DirectoryNotFoundException($"QGIS install folder not found: {resolvedFolder}");
        }

        var directBinPath = Path.Combine(resolvedFolder, "bin", executableName);
        if (File.Exists(directBinPath))
        {
            return directBinPath;
        }

        var alreadyBinPath = Path.Combine(resolvedFolder, executableName);
        if (File.Exists(alreadyBinPath) &&
            string.Equals(Path.GetFileName(Path.GetDirectoryName(alreadyBinPath)), "bin", StringComparison.OrdinalIgnoreCase))
        {
            return alreadyBinPath;
        }

        throw new FileNotFoundException(
            $"Could not find {executableName} inside the selected QGIS installation. Expected it at <QGIS>/bin/{executableName}.",
            directBinPath);
    }

    private static string RunProcess(string executable, string arguments, string toolLabel)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using (var process = Process.Start(startInfo))
        {
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {toolLabel}.");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{toolLabel} failed with exit code {process.ExitCode}.\nCommand: {executable} {arguments}\n{stdout}\n{stderr}");
            }

            return stdout;
        }
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    private static string ResolveOptionalPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : ResolvePath(path);
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + ((b - a) * t);
    }

    private static double LongitudeToMeters(double longitude, double originLongitude, double referenceLatitude)
    {
        const double earthRadius = 6378137d;
        var deltaLongitudeRadians = DegreesToRadians(longitude - originLongitude);
        var latitudeRadians = DegreesToRadians(referenceLatitude);
        return earthRadius * deltaLongitudeRadians * Math.Cos(latitudeRadians);
    }

    private static double LatitudeToMeters(double latitude, double originLatitude)
    {
        const double earthRadius = 6378137d;
        var deltaLatitudeRadians = DegreesToRadians(latitude - originLatitude);
        return earthRadius * deltaLatitudeRadians;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * (Math.PI / 180d);
    }

    private static TileBounds ParseWgs84Bounds(string gdalInfoJson)
    {
        var wgsSectionIndex = gdalInfoJson.IndexOf("\"wgs84Extent\"", StringComparison.OrdinalIgnoreCase);
        if (wgsSectionIndex < 0)
        {
            throw new InvalidOperationException("Could not find wgs84Extent in GDAL output.");
        }

        var coordinatesIndex = gdalInfoJson.IndexOf("\"coordinates\"", wgsSectionIndex, StringComparison.OrdinalIgnoreCase);
        if (coordinatesIndex < 0)
        {
            throw new InvalidOperationException("Could not find coordinates in GDAL output.");
        }

        var arrayStart = gdalInfoJson.IndexOf('[', coordinatesIndex);
        if (arrayStart < 0)
        {
            throw new InvalidOperationException("Could not parse coordinates array from GDAL output.");
        }

        var coordinateBlock = ExtractBracketBlock(gdalInfoJson, arrayStart);
        var matches = Regex.Matches(coordinateBlock, @"-?\d+(?:\.\d+)?");
        if (matches.Count < 4 || matches.Count % 2 != 0)
        {
            throw new InvalidOperationException("Unexpected coordinate data in GDAL output.");
        }

        var west = double.PositiveInfinity;
        var east = double.NegativeInfinity;
        var south = double.PositiveInfinity;
        var north = double.NegativeInfinity;

        for (var i = 0; i < matches.Count; i += 2)
        {
            var lon = double.Parse(matches[i].Value, CultureInfo.InvariantCulture);
            var lat = double.Parse(matches[i + 1].Value, CultureInfo.InvariantCulture);
            west = Math.Min(west, lon);
            east = Math.Max(east, lon);
            south = Math.Min(south, lat);
            north = Math.Max(north, lat);
        }

        return new TileBounds(north, south, west, east);
    }

    private static string ExtractBracketBlock(string text, int startIndex)
    {
        var depth = 0;
        for (var i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '[')
            {
                depth++;
            }
            else if (text[i] == ']')
            {
                depth--;
                if (depth == 0)
                {
                    return text.Substring(startIndex, i - startIndex + 1);
                }
            }
        }

        throw new InvalidOperationException("Unbalanced coordinate array in GDAL output.");
    }

    private readonly struct TileBounds
    {
        public readonly double north;
        public readonly double south;
        public readonly double west;
        public readonly double east;

        public TileBounds(double north, double south, double west, double east)
        {
            this.north = north;
            this.south = south;
            this.west = west;
            this.east = east;
        }
    }

    private sealed class EnviFloatRaster
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public float[] Values { get; private set; }

        public static EnviFloatRaster Read(string binaryPath)
        {
            var headerPath = Path.ChangeExtension(binaryPath, ".hdr");
            if (!File.Exists(headerPath))
            {
                throw new FileNotFoundException("ENVI header file not found.", headerPath);
            }

            var metadata = ParseHeader(headerPath);
            var width = ParseInt(metadata, "samples");
            var height = ParseInt(metadata, "lines");
            var bands = ParseInt(metadata, "bands");
            var dataType = ParseInt(metadata, "data type");
            var byteOrder = metadata.TryGetValue("byte order", out var byteOrderValue) ? ParseInt(byteOrderValue) : 0;

            if (bands != 1)
            {
                throw new InvalidOperationException("Only single-band ENVI rasters are supported.");
            }

            if (dataType != 4)
            {
                throw new InvalidOperationException("Expected ENVI Float32 raster (data type = 4).");
            }

            var values = new float[width * height];
            using (var stream = File.OpenRead(binaryPath))
            using (var reader = new BinaryReader(stream))
            {
                for (var i = 0; i < values.Length; i++)
                {
                    var bytes = reader.ReadBytes(sizeof(float));
                    if (bytes.Length < sizeof(float))
                    {
                        throw new EndOfStreamException("Unexpected end of ENVI float raster.");
                    }

                    var littleEndian = byteOrder == 0;
                    if (BitConverter.IsLittleEndian != littleEndian)
                    {
                        Array.Reverse(bytes);
                    }

                    values[i] = BitConverter.ToSingle(bytes, 0);
                }
            }

            return new EnviFloatRaster
            {
                Width = width,
                Height = height,
                Values = values
            };
        }

        public float GetValue(int y, int x)
        {
            return Values[(y * Width) + x];
        }

        public void RemapValuesOutsideLandMask(byte[] landMaskValues, float waterElevation)
        {
            if (landMaskValues == null || landMaskValues.Length != Values.Length)
            {
                throw new InvalidOperationException("The GSHHG mask size does not match the DEM raster size.");
            }

            for (var i = 0; i < Values.Length; i++)
            {
                var value = Values[i];
                if (landMaskValues[i] == 0 || float.IsNaN(value) || float.IsInfinity(value))
                {
                    Values[i] = waterElevation;
                }
            }
        }

        private static Dictionary<string, string> ParseHeader(string headerPath)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(headerPath);
            foreach (var line in lines)
            {
                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, separatorIndex).Trim();
                var value = line.Substring(separatorIndex + 1).Trim().Trim('{', '}');
                metadata[key] = value;
            }

            return metadata;
        }

        private static int ParseInt(Dictionary<string, string> metadata, string key)
        {
            if (!metadata.TryGetValue(key, out var value))
            {
                throw new InvalidOperationException($"Missing ENVI header value '{key}'.");
            }

            return ParseInt(value);
        }

        private static int ParseInt(string value)
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }
    }

    private sealed class EnviByteRaster
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public byte[] Values { get; private set; }

        public static EnviByteRaster Read(string binaryPath)
        {
            var headerPath = Path.ChangeExtension(binaryPath, ".hdr");
            if (!File.Exists(headerPath))
            {
                throw new FileNotFoundException("ENVI header file not found.", headerPath);
            }

            var metadata = ParseHeader(headerPath);
            var width = ParseInt(metadata, "samples");
            var height = ParseInt(metadata, "lines");
            var bands = ParseInt(metadata, "bands");
            var dataType = ParseInt(metadata, "data type");

            if (bands != 1)
            {
                throw new InvalidOperationException("Only single-band ENVI rasters are supported.");
            }

            if (dataType != 1)
            {
                throw new InvalidOperationException("Expected ENVI Byte raster (data type = 1).");
            }

            var values = File.ReadAllBytes(binaryPath);
            if (values.Length < width * height)
            {
                throw new EndOfStreamException("Unexpected end of ENVI byte raster.");
            }

            return new EnviByteRaster
            {
                Width = width,
                Height = height,
                Values = values
            };
        }

        private static Dictionary<string, string> ParseHeader(string headerPath)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(headerPath);
            foreach (var line in lines)
            {
                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, separatorIndex).Trim();
                var value = line.Substring(separatorIndex + 1).Trim().Trim('{', '}');
                metadata[key] = value;
            }

            return metadata;
        }

        private static int ParseInt(Dictionary<string, string> metadata, string key)
        {
            if (!metadata.TryGetValue(key, out var value))
            {
                throw new InvalidOperationException($"Missing ENVI header value '{key}'.");
            }

            return int.Parse(value, CultureInfo.InvariantCulture);
        }
    }
}
