using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class TerrainForgerGisDataUtility
{
    private const string TerrainRootAssetPath = "Assets/Terrain";
    private const string GeoTiffAssetPath = "Assets/Terrain/GeoTIFF";
    private const string SatAssetPath = "Assets/Terrain/SAT";
    private const int DefaultDemPreviewSize = 512;
    private const int MapboxMaxStaticImageSize = 1280;
    private const int GoogleMapsDefaultTileSize = 256;
    private const int GoogleMapsMaxZoomLevel = 22;
    private const string DemDownloadProgressTitle = "TerrainForger DEM Download";
    private const string SatDownloadProgressTitle = "TerrainForger Satellite Download";

    public static void StoreLocalSource(TerrainForgeWorkflowSettings settings)
    {
        if (settings.localSourceType == TerrainForgerLocalSourceType.None)
        {
            throw new InvalidOperationException("Choose a local source type before loading a file.");
        }

        if (string.IsNullOrWhiteSpace(settings.localSourcePath))
        {
            throw new InvalidOperationException("Choose a local GeoTIFF or KAP file first.");
        }

        var sourceFullPath = TerrainForgeWindowUtility.ResolveFolderPath(settings.localSourcePath);
        if (!File.Exists(sourceFullPath))
        {
            throw new FileNotFoundException("Source file not found.", sourceFullPath);
        }

        EnsureTerrainFolders();
        var destinationAssetPath = settings.localSourceType == TerrainForgerLocalSourceType.GeoTiff
            ? CombineAssetPath(GeoTiffAssetPath, Path.GetFileName(sourceFullPath))
            : CombineAssetPath(SatAssetPath, Path.GetFileName(sourceFullPath));

        var destinationFullPath = ToAbsoluteProjectPath(destinationAssetPath);
        File.Copy(sourceFullPath, destinationFullPath, true);

        if (settings.localSourceType == TerrainForgerLocalSourceType.GeoTiff)
        {
            settings.geoTiffPath = destinationAssetPath;
            settings.lastDemGeoTiffPath = destinationAssetPath;
        }
        else
        {
            settings.satelliteGeoTiffPath = destinationAssetPath;
            settings.lastSatelliteImagePath = destinationAssetPath;
        }

        AssetDatabase.Refresh();
        settings.SaveSettings();
    }

    public static void RefillBoundsFromLocalSource(TerrainForgeWorkflowSettings settings)
    {
        if (settings.localSourceType == TerrainForgerLocalSourceType.None)
        {
            throw new InvalidOperationException("Choose a local source type before refilling bounds.");
        }

        if (string.IsNullOrWhiteSpace(settings.localSourcePath))
        {
            throw new InvalidOperationException("Choose a local GeoTIFF or KAP file first.");
        }

        var sourceFullPath = TerrainForgeWindowUtility.ResolveFolderPath(settings.localSourcePath);
        if (!File.Exists(sourceFullPath))
        {
            throw new FileNotFoundException("Source file not found.", sourceFullPath);
        }

        TryFillBoundsFromSpatialFile(settings, sourceFullPath);
    }

    public static void DownloadDem(TerrainForgeWorkflowSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.demProviderId))
        {
            throw new InvalidOperationException("Select a DEM provider first.");
        }

        EnsureTerrainFolders();

        switch (settings.demProviderId)
        {
            case TerrainDataProviderIds.OpenTopography:
                DownloadOpenTopographyDem(settings);
                return;
            default:
                throw new InvalidOperationException($"Unsupported DEM provider id: {settings.demProviderId}");
        }
    }

    public static void DownloadSatellite(TerrainForgeWorkflowSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.imageryProviderId))
        {
            throw new InvalidOperationException("Select a satellite imagery provider first.");
        }

        EnsureTerrainFolders();

        switch (settings.imageryProviderId)
        {
            case TerrainDataProviderIds.Mapbox:
                DownloadMapboxSatellite(settings);
                return;
            case TerrainDataProviderIds.GoogleMapsPlatform:
                DownloadGoogleMapsSatellite(settings);
                return;
            default:
                throw new InvalidOperationException($"Unsupported imagery provider id: {settings.imageryProviderId}");
        }
    }

    public static TerrainForgerSatelliteDownloadPlan BuildSatelliteDownloadPlan(TerrainForgeWorkflowSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.imageryProviderId))
        {
            return new TerrainForgerSatelliteDownloadPlan
            {
                warningMessage = "Select a satellite imagery provider first."
            };
        }

        var west = settings.westBound.ToDecimalDegrees();
        var south = settings.southBound.ToDecimalDegrees();
        var east = settings.eastBound.ToDecimalDegrees();
        var north = settings.northBound.ToDecimalDegrees();

        var widthMeters = Math.Abs(LongitudeToWebMercatorX(east) - LongitudeToWebMercatorX(west));
        var heightMeters = Math.Abs(LatitudeToWebMercatorY(north) - LatitudeToWebMercatorY(south));

        if (widthMeters <= 0d || heightMeters <= 0d)
        {
            return new TerrainForgerSatelliteDownloadPlan
            {
                providerId = settings.imageryProviderId,
                warningMessage = "Map bounds must describe a non-zero area."
            };
        }

        var pixelsPerMeter = ResolvePixelsPerMeter(settings.satelliteResolutionUnit, settings.satelliteResolution);
        if (pixelsPerMeter <= 0d || double.IsNaN(pixelsPerMeter) || double.IsInfinity(pixelsPerMeter))
        {
            return new TerrainForgerSatelliteDownloadPlan
            {
                providerId = settings.imageryProviderId,
                warningMessage = "Satellite resolution must be greater than zero."
            };
        }

        if (string.Equals(settings.imageryProviderId, TerrainDataProviderIds.GoogleMapsPlatform, StringComparison.OrdinalIgnoreCase))
        {
            var centerLatitude = (north + south) * 0.5d;
            var desiredMetersPerPixel = 1d / pixelsPerMeter;
            var zoomLevel = ResolveGoogleZoomLevel(desiredMetersPerPixel, centerLatitude);
            var actualMetersPerPixel = ResolveGoogleMetersPerPixel(centerLatitude, zoomLevel);
            var actualPixelsPerMeter = 1d / actualMetersPerPixel;
            var googleTileSize = ResolveProviderMaxTileSize(settings.imageryProviderId);
            var tilesPerAxis = 1 << zoomLevel;
            var firstTileX = Mathf.Clamp((int)Math.Floor(LongitudeToTileX(west, zoomLevel)), 0, tilesPerAxis - 1);
            var lastTileX = Mathf.Clamp((int)Math.Floor(LongitudeToTileX(east, zoomLevel) - 1e-9d), 0, tilesPerAxis - 1);
            var firstTileY = Mathf.Clamp((int)Math.Floor(LatitudeToTileY(north, zoomLevel)), 0, tilesPerAxis - 1);
            var lastTileY = Mathf.Clamp((int)Math.Floor(LatitudeToTileY(south, zoomLevel) - 1e-9d), 0, tilesPerAxis - 1);
            var googleTilesX = Mathf.Max(1, lastTileX - firstTileX + 1);
            var googleTilesY = Mathf.Max(1, lastTileY - firstTileY + 1);
            var googleTotalWidthPixels = googleTilesX * googleTileSize;
            var googleTotalHeightPixels = googleTilesY * googleTileSize;
            var googleTotalTiles = googleTilesX * googleTilesY;
            var googleWarningMessage = $"Google Maps uses zoom level {zoomLevel} for this request, which yields approximately {actualMetersPerPixel:0.####} m/px at the map center.";

            if (googleTotalTiles > 1)
            {
                googleWarningMessage = $"{googleWarningMessage} The download will use a {googleTilesX} x {googleTilesY} mosaic ({googleTotalTiles} tiles).";
            }

            if (googleTotalTiles > 64)
            {
                googleWarningMessage = $"{googleWarningMessage} Large request: {googleTotalTiles} tiles may take a while and consume significant provider quota.";
            }

            return new TerrainForgerSatelliteDownloadPlan
            {
                isValid = true,
                providerId = settings.imageryProviderId,
                widthMeters = widthMeters,
                heightMeters = heightMeters,
                pixelsPerMeter = actualPixelsPerMeter,
                metersPerPixel = actualMetersPerPixel,
                totalWidthPixels = googleTotalWidthPixels,
                totalHeightPixels = googleTotalHeightPixels,
                maxTileSize = googleTileSize,
                tilesX = googleTilesX,
                tilesY = googleTilesY,
                totalTiles = googleTotalTiles,
                maxTileWidthPixels = googleTileSize,
                maxTileHeightPixels = googleTileSize,
                requiresTiling = googleTotalTiles > 1,
                zoomLevel = zoomLevel,
                tilePixelWidth = googleTileSize,
                tilePixelHeight = googleTileSize,
                firstTileX = firstTileX,
                lastTileX = lastTileX,
                firstTileY = firstTileY,
                lastTileY = lastTileY,
                warningMessage = googleWarningMessage
            };
        }

        var totalWidthPixels = Mathf.Max(1, Mathf.CeilToInt((float)(widthMeters * pixelsPerMeter)));
        var totalHeightPixels = Mathf.Max(1, Mathf.CeilToInt((float)(heightMeters * pixelsPerMeter)));
        var maxTileSize = ResolveProviderMaxTileSize(settings.imageryProviderId);
        var tilesX = Mathf.Max(1, Mathf.CeilToInt(totalWidthPixels / (float)maxTileSize));
        var tilesY = Mathf.Max(1, Mathf.CeilToInt(totalHeightPixels / (float)maxTileSize));
        var maxTileWidthPixels = Mathf.Max(1, Mathf.CeilToInt(totalWidthPixels / (float)tilesX));
        var maxTileHeightPixels = Mathf.Max(1, Mathf.CeilToInt(totalHeightPixels / (float)tilesY));
        var totalTiles = tilesX * tilesY;

        var warningMessage = string.Empty;
        if (totalTiles > 1)
        {
            warningMessage = $"{ResolveProviderDisplayName(settings.imageryProviderId)} requires tiling for this resolution. The download will use a {tilesX} x {tilesY} mosaic ({totalTiles} tiles).";
        }

        if (totalTiles > 64)
        {
            warningMessage = $"{warningMessage} Large request: {totalTiles} tiles may take a while and consume significant provider quota.".Trim();
        }

        return new TerrainForgerSatelliteDownloadPlan
        {
            isValid = true,
            providerId = settings.imageryProviderId,
            widthMeters = widthMeters,
            heightMeters = heightMeters,
            pixelsPerMeter = pixelsPerMeter,
            metersPerPixel = 1d / pixelsPerMeter,
            totalWidthPixels = totalWidthPixels,
            totalHeightPixels = totalHeightPixels,
            maxTileSize = maxTileSize,
            tilesX = tilesX,
            tilesY = tilesY,
            totalTiles = totalTiles,
            maxTileWidthPixels = maxTileWidthPixels,
            maxTileHeightPixels = maxTileHeightPixels,
            requiresTiling = totalTiles > 1,
            zoomLevel = -1,
            tilePixelWidth = maxTileWidthPixels,
            tilePixelHeight = maxTileHeightPixels,
            warningMessage = warningMessage
        };
    }

    public static void DownloadSelectedData(TerrainForgeWorkflowSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.demProviderId))
        {
            DownloadDem(settings);
        }

        if (!string.IsNullOrWhiteSpace(settings.imageryProviderId))
        {
            DownloadSatellite(settings);
        }
    }

    private static void DownloadOpenTopographyDem(TerrainForgeWorkflowSettings settings)
    {
        var apiKey = TerrainDataServiceSettings.instance.OpenTopographyApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenTopography API key is not configured in Terrain Data Services.");
        }

        const string demType = "COP30";
        var url =
            "https://portal.opentopography.org/API/globaldem" +
            $"?demtype={Uri.EscapeDataString(demType)}" +
            $"&south={FormatDouble(settings.southBound.ToDecimalDegrees())}" +
            $"&north={FormatDouble(settings.northBound.ToDecimalDegrees())}" +
            $"&west={FormatDouble(settings.westBound.ToDecimalDegrees())}" +
            $"&east={FormatDouble(settings.eastBound.ToDecimalDegrees())}" +
            "&outputFormat=GTiff" +
            $"&API_Key={Uri.EscapeDataString(apiKey)}";

        var outputFileName = $"dem_{demType}_{DateTime.Now:yyyyMMdd_HHmmss}.tif";
        var outputAssetPath = CombineAssetPath(GeoTiffAssetPath, outputFileName);
        try
        {
            EditorUtility.DisplayProgressBar(DemDownloadProgressTitle, "Downloading DEM GeoTIFF from OpenTopography...", 0.25f);
            DownloadToFile(url, ToAbsoluteProjectPath(outputAssetPath));
            EditorUtility.DisplayProgressBar(DemDownloadProgressTitle, "Finalizing DEM asset...", 0.9f);

            settings.geoTiffPath = outputAssetPath;
            settings.lastDemGeoTiffPath = outputAssetPath;
            AssetDatabase.Refresh();
            settings.SaveSettings();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    public static void TryFillBoundsFromSpatialFile(TerrainForgeWorkflowSettings settings, string filePath)
    {
        var gdalInfoPath = ResolveQgisExecutable("gdalinfo.exe");
        var output = RunProcess(gdalInfoPath, $"-json \"{filePath}\"");
        var bounds = ParseWgs84Bounds(output);

        settings.westBound = LongitudeDdm.FromDecimalDegrees(bounds.west);
        settings.eastBound = LongitudeDdm.FromDecimalDegrees(bounds.east);
        settings.southBound = LatitudeDdm.FromDecimalDegrees(bounds.south);
        settings.northBound = LatitudeDdm.FromDecimalDegrees(bounds.north);
        settings.SaveSettings();
    }

    public static Texture2D CreateDemPreviewTexture(string geoTiffPath, int previewWidth = DefaultDemPreviewSize)
    {
        return CreateRasterPreviewTexture(geoTiffPath, "DEM Preview", previewWidth);
    }

    public static Texture2D CreateSourcePreviewTexture(string sourcePath, int previewWidth = DefaultDemPreviewSize)
    {
        return CreateSourcePreviewTextureInternal(sourcePath, previewWidth);
    }

    private static Texture2D CreateRasterPreviewTexture(string rasterPath, string label, int previewWidth = DefaultDemPreviewSize)
    {
        if (string.IsNullOrWhiteSpace(rasterPath))
        {
            throw new InvalidOperationException($"{label} path is required.");
        }

        var sourcePath = ToAbsoluteProjectPath(rasterPath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"File not found for {label.ToLowerInvariant()}.", sourcePath);
        }

        var gdalTranslatePath = ResolveQgisExecutable("gdal_translate.exe");
        var tempPreviewPath = Path.Combine(Path.GetTempPath(), $"terrainforger_dem_preview_{Guid.NewGuid():N}.png");
        var safePreviewWidth = Mathf.Clamp(previewWidth, 64, 2048);

        try
        {
            var arguments = string.Join(" ", new[]
            {
                "-of PNG",
                "-ot Byte",
                "-outsize",
                safePreviewWidth.ToString(CultureInfo.InvariantCulture),
                "0",
                "-scale",
                Quote(sourcePath),
                Quote(tempPreviewPath)
            });

            RunProcess(gdalTranslatePath, arguments);

            var pngBytes = File.ReadAllBytes(tempPreviewPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
            {
                name = $"{label} ({Path.GetFileName(sourcePath)})",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            if (!texture.LoadImage(pngBytes, markNonReadable: false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException($"Failed to decode the generated {label.ToLowerInvariant()} image.");
            }

            return texture;
        }
        finally
        {
            if (File.Exists(tempPreviewPath))
            {
                File.Delete(tempPreviewPath);
            }
        }
    }

    private static Texture2D CreateSourcePreviewTextureInternal(string rasterPath, int previewWidth = DefaultDemPreviewSize)
    {
        if (string.IsNullOrWhiteSpace(rasterPath))
        {
            throw new InvalidOperationException("Source Preview path is required.");
        }

        var sourcePath = ToAbsoluteProjectPath(rasterPath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("File not found for source preview.", sourcePath);
        }

        var gdalTranslatePath = ResolveQgisExecutable("gdal_translate.exe");
        var gdalInfoPath = ResolveQgisExecutable("gdalinfo.exe");
        var tempPreviewPath = Path.Combine(Path.GetTempPath(), $"terrainforger_source_preview_{Guid.NewGuid():N}.jpg");
        var safePreviewWidth = Mathf.Clamp(previewWidth, 64, 2048);

        try
        {
            var infoJson = RunProcess(gdalInfoPath, $"-json {Quote(sourcePath)}");
            var expandPalette = RequiresPaletteExpansion(infoJson);
            var arguments = expandPalette
                ? string.Join(" ", new[]
                {
                    "-of JPEG",
                    "-expand rgb",
                    "-outsize",
                    safePreviewWidth.ToString(CultureInfo.InvariantCulture),
                    "0",
                    Quote(sourcePath),
                    Quote(tempPreviewPath)
                })
                : string.Join(" ", new[]
                {
                    "-of JPEG",
                    "-outsize",
                    safePreviewWidth.ToString(CultureInfo.InvariantCulture),
                    "0",
                    Quote(sourcePath),
                    Quote(tempPreviewPath)
                });

            RunProcess(gdalTranslatePath, arguments);

            var imageBytes = File.ReadAllBytes(tempPreviewPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
            {
                name = $"Source Preview ({Path.GetFileName(sourcePath)})",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            if (!texture.LoadImage(imageBytes, markNonReadable: false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException("Failed to decode the generated source preview image.");
            }

            return texture;
        }
        finally
        {
            if (File.Exists(tempPreviewPath))
            {
                File.Delete(tempPreviewPath);
            }
        }
    }

    private static void DownloadMapboxSatellite(TerrainForgeWorkflowSettings settings)
    {
        var accessToken = TerrainDataServiceSettings.instance.MapboxAccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Mapbox access token is not configured in Terrain Data Services.");
        }

        var plan = BuildSatelliteDownloadPlan(settings);
        if (!plan.isValid)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(plan.warningMessage)
                ? "Unable to build a valid satellite download plan."
                : plan.warningMessage);
        }

        var west = settings.westBound.ToDecimalDegrees();
        var south = settings.southBound.ToDecimalDegrees();
        var east = settings.eastBound.ToDecimalDegrees();
        var north = settings.northBound.ToDecimalDegrees();
        var westMercator = LongitudeToWebMercatorX(west);
        var southMercator = LatitudeToWebMercatorY(south);
        var eastMercator = LongitudeToWebMercatorX(east);
        var northMercator = LatitudeToWebMercatorY(north);
        var tempRoot = Path.Combine(Path.GetTempPath(), $"terrainforger_mapbox_tiles_{Guid.NewGuid():N}");
        var outputFileName = $"sat_mapbox_{DateTime.Now:yyyyMMdd_HHmmss}.tif";
        var outputAssetPath = CombineAssetPath(SatAssetPath, outputFileName);
        var outputFullPath = ToAbsoluteProjectPath(outputAssetPath);
        var previousSatelliteImagePath = settings.lastSatelliteImagePath;

        try
        {
            Directory.CreateDirectory(tempRoot);
            DownloadAndWarpMapboxTiles(accessToken, plan, tempRoot, outputFullPath, west, south, east, north, westMercator, southMercator, eastMercator, northMercator);
            DeletePreviousSatelliteImages(previousSatelliteImagePath);

            settings.satelliteGeoTiffPath = outputAssetPath;
            settings.lastSatelliteImagePath = outputAssetPath;
            AssetDatabase.Refresh();
            settings.SaveSettings();
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

    private static void DownloadGoogleMapsSatellite(TerrainForgeWorkflowSettings settings)
    {
        var apiKey = TerrainDataServiceSettings.instance.GoogleMapsApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Google Maps API key is not configured in Terrain Data Services.");
        }

        var plan = BuildSatelliteDownloadPlan(settings);
        if (!plan.isValid)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(plan.warningMessage)
                ? "Unable to build a valid satellite download plan."
                : plan.warningMessage);
        }

        var session = CreateGoogleMapsSatelliteSession(apiKey);
        var west = settings.westBound.ToDecimalDegrees();
        var south = settings.southBound.ToDecimalDegrees();
        var east = settings.eastBound.ToDecimalDegrees();
        var north = settings.northBound.ToDecimalDegrees();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"terrainforger_google_tiles_{Guid.NewGuid():N}");
        var outputFileName = $"sat_google_{DateTime.Now:yyyyMMdd_HHmmss}.tif";
        var outputAssetPath = CombineAssetPath(SatAssetPath, outputFileName);
        var outputFullPath = ToAbsoluteProjectPath(outputAssetPath);
        var previousSatelliteImagePath = settings.lastSatelliteImagePath;

        try
        {
            Directory.CreateDirectory(tempRoot);
            DownloadAndWarpGoogleTiles(apiKey, session, plan, tempRoot, outputFullPath, west, south, east, north);
            DeletePreviousSatelliteImages(previousSatelliteImagePath);

            settings.satelliteGeoTiffPath = outputAssetPath;
            settings.lastSatelliteImagePath = outputAssetPath;
            AssetDatabase.Refresh();
            settings.SaveSettings();
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

    private static TerrainForgerGoogleMapsSessionResponse CreateGoogleMapsSatelliteSession(string apiKey)
    {
        var request = new TerrainForgerGoogleMapsSessionRequest
        {
            mapType = "satellite",
            language = ResolveGoogleMapsLanguage(),
            region = ResolveGoogleMapsRegion(),
            imageFormat = "png"
        };

        var responseJson = UploadJson(
            $"https://tile.googleapis.com/v1/createSession?key={Uri.EscapeDataString(apiKey)}",
            JsonUtility.ToJson(request));
        var response = JsonUtility.FromJson<TerrainForgerGoogleMapsSessionResponse>(responseJson);
        if (response == null || string.IsNullOrWhiteSpace(response.session))
        {
            throw new InvalidOperationException("Google Maps session request did not return a valid session token.");
        }

        if (response.tileWidth <= 0)
        {
            response.tileWidth = GoogleMapsDefaultTileSize;
        }

        if (response.tileHeight <= 0)
        {
            response.tileHeight = response.tileWidth;
        }

        if (string.IsNullOrWhiteSpace(response.imageFormat))
        {
            response.imageFormat = "png";
        }

        return response;
    }

    private static string ResolveQgisExecutable(string executableName)
    {
        var qgisInstallFolder = TerrainDataServiceSettings.instance.QgisInstallFolder;
        if (string.IsNullOrWhiteSpace(qgisInstallFolder))
        {
            throw new InvalidOperationException($"QGIS install folder is required to use {executableName}. Set it in TerrainForger Data Services.");
        }

        var resolvedFolder = TerrainForgeWindowUtility.ResolveFolderPath(qgisInstallFolder);
        var executablePath = Path.Combine(resolvedFolder, "bin", executableName);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"Could not find {executableName} inside the selected QGIS installation.", executablePath);
        }

        return executablePath;
    }

    private static void GeoreferenceAndWarpWebMercatorImage(
        string inputImagePath,
        string tempMercatorPath,
        string outputGeoTiffPath,
        double west,
        double south,
        double east,
        double north)
    {
        var mercatorWest = LongitudeToWebMercatorX(west);
        var mercatorEast = LongitudeToWebMercatorX(east);
        var mercatorSouth = LatitudeToWebMercatorY(south);
        var mercatorNorth = LatitudeToWebMercatorY(north);

        var gdalTranslatePath = ResolveQgisExecutable("gdal_translate.exe");
        var gdalWarpPath = ResolveQgisExecutable("gdalwarp.exe");

        var translateArgs = string.Join(" ", new[]
        {
            "-of GTiff",
            "-a_srs EPSG:3857",
            "-a_ullr",
            FormatDouble(mercatorWest),
            FormatDouble(mercatorNorth),
            FormatDouble(mercatorEast),
            FormatDouble(mercatorSouth),
            Quote(inputImagePath),
            Quote(tempMercatorPath)
        });

        RunProcess(gdalTranslatePath, translateArgs);

        var warpArgs = string.Join(" ", new[]
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
            "-of GTiff",
            Quote(tempMercatorPath),
            Quote(outputGeoTiffPath)
        });

        RunProcess(gdalWarpPath, warpArgs);
    }

    private static string RunProcess(string executable, string arguments)
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
                throw new InvalidOperationException($"Failed to start process '{executable}'.");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{Path.GetFileName(executable)} failed with exit code {process.ExitCode}.\n{stdout}\n{stderr}");
            }

            return stdout;
        }
    }

    private static (double west, double east, double south, double north) ParseWgs84Bounds(string gdalInfoJson)
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

        return (west, east, south, north);
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

    private static bool RequiresPaletteExpansion(string gdalInfoJson)
    {
        return gdalInfoJson.IndexOf("\"colorInterpretation\":\"Palette\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
               gdalInfoJson.IndexOf("\"colorInterpretation\": \"Palette\"", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void DownloadToFile(string url, string absoluteOutputPath)
    {
        using (var client = new WebClient())
        {
            client.Headers.Add(HttpRequestHeader.UserAgent, "TerrainForger/1.0");
            client.DownloadFile(url, absoluteOutputPath);
        }

        Debug.Log($"[TerrainForger GIS] Saved data to {absoluteOutputPath}");
    }

    private static string UploadJson(string url, string jsonBody)
    {
        using (var client = new WebClient())
        {
            client.Encoding = Encoding.UTF8;
            client.Headers.Add(HttpRequestHeader.UserAgent, "TerrainForger/1.0");
            client.Headers[HttpRequestHeader.ContentType] = "application/json";
            return client.UploadString(url, "POST", jsonBody);
        }
    }

    private static void EnsureTerrainFolders()
    {
        Directory.CreateDirectory(ToAbsoluteProjectPath(TerrainRootAssetPath));
        Directory.CreateDirectory(ToAbsoluteProjectPath(GeoTiffAssetPath));
        Directory.CreateDirectory(ToAbsoluteProjectPath(SatAssetPath));
        AssetDatabase.Refresh();
    }

    private static string CombineAssetPath(string folderAssetPath, string fileName)
    {
        return $"{folderAssetPath.TrimEnd('/')}/{fileName}";
    }

    private static string ToAbsoluteProjectPath(string assetPath)
    {
        if (Path.IsPathRooted(assetPath))
        {
            return assetPath;
        }

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private static void DownloadAndWarpMapboxTiles(
        string accessToken,
        TerrainForgerSatelliteDownloadPlan plan,
        string tempRoot,
        string outputGeoTiffPath,
        double west,
        double south,
        double east,
        double north,
        double westMercator,
        double southMercator,
        double eastMercator,
        double northMercator)
    {
        var mercatorWidth = eastMercator - westMercator;
        var mercatorHeight = northMercator - southMercator;
        var tileGeoTiffs = new string[plan.totalTiles];
        var tileIndex = 0;

        for (var row = 0; row < plan.tilesY; row++)
        {
            for (var col = 0; col < plan.tilesX; col++)
            {
                var currentTileNumber = tileIndex + 1;
                var progressStart = tileIndex / (float)Math.Max(1, plan.totalTiles);
                var xPixelStart = Mathf.RoundToInt(plan.totalWidthPixels * (col / (float)plan.tilesX));
                var xPixelEnd = Mathf.RoundToInt(plan.totalWidthPixels * ((col + 1) / (float)plan.tilesX));
                var yPixelStart = Mathf.RoundToInt(plan.totalHeightPixels * (row / (float)plan.tilesY));
                var yPixelEnd = Mathf.RoundToInt(plan.totalHeightPixels * ((row + 1) / (float)plan.tilesY));

                var tileWidthPixels = Mathf.Max(1, xPixelEnd - xPixelStart);
                var tileHeightPixels = Mathf.Max(1, yPixelEnd - yPixelStart);

                var tileWestMercator = westMercator + (mercatorWidth * (xPixelStart / (double)plan.totalWidthPixels));
                var tileEastMercator = westMercator + (mercatorWidth * (xPixelEnd / (double)plan.totalWidthPixels));
                var tileNorthMercator = northMercator - (mercatorHeight * (yPixelStart / (double)plan.totalHeightPixels));
                var tileSouthMercator = northMercator - (mercatorHeight * (yPixelEnd / (double)plan.totalHeightPixels));

                var tileWest = WebMercatorXToLongitude(tileWestMercator);
                var tileEast = WebMercatorXToLongitude(tileEastMercator);
                var tileSouth = WebMercatorYToLatitude(tileSouthMercator);
                var tileNorth = WebMercatorYToLatitude(tileNorthMercator);

                var bbox = $"[{FormatDouble(tileWest)},{FormatDouble(tileSouth)},{FormatDouble(tileEast)},{FormatDouble(tileNorth)}]";
                var url =
                    "https://api.mapbox.com/styles/v1/mapbox/satellite-v9/static/" +
                    bbox +
                    $"/{tileWidthPixels}x{tileHeightPixels}" +
                    $"?access_token={Uri.EscapeDataString(accessToken)}";

                var tempImagePath = Path.Combine(tempRoot, $"tile_{row}_{col}.jpg");
                var tempMercatorPath = Path.Combine(tempRoot, $"tile_{row}_{col}_mercator.tif");
                var tempGeoTiffPath = Path.Combine(tempRoot, $"tile_{row}_{col}.tif");

                EditorUtility.DisplayProgressBar(
                    SatDownloadProgressTitle,
                    $"Downloading tile {currentTileNumber} of {plan.totalTiles}...",
                    Mathf.Lerp(0.05f, 0.65f, progressStart));
                DownloadToFile(url, tempImagePath);

                EditorUtility.DisplayProgressBar(
                    SatDownloadProgressTitle,
                    $"Georeferencing tile {currentTileNumber} of {plan.totalTiles}...",
                    Mathf.Lerp(0.1f, 0.8f, progressStart + (0.5f / Math.Max(1, plan.totalTiles))));
                GeoreferenceAndWarpWebMercatorImage(
                    tempImagePath,
                    tempMercatorPath,
                    tempGeoTiffPath,
                    tileWest,
                    tileSouth,
                    tileEast,
                    tileNorth);

                tileGeoTiffs[tileIndex++] = tempGeoTiffPath;
            }
        }

        if (tileGeoTiffs.Length == 1)
        {
            EditorUtility.DisplayProgressBar(SatDownloadProgressTitle, "Finalizing satellite GeoTIFF...", 0.95f);
            File.Copy(tileGeoTiffs[0], outputGeoTiffPath, true);
            return;
        }

        BuildGeoTiffMosaic(tempRoot, "mapbox_satellite.vrt", tileGeoTiffs, outputGeoTiffPath, west, south, east, north);
    }

    private static void DownloadAndWarpGoogleTiles(
        string apiKey,
        TerrainForgerGoogleMapsSessionResponse session,
        TerrainForgerSatelliteDownloadPlan plan,
        string tempRoot,
        string outputGeoTiffPath,
        double west,
        double south,
        double east,
        double north)
    {
        var tileGeoTiffs = new string[plan.totalTiles];
        var tileIndex = 0;

        for (var tileY = plan.firstTileY; tileY <= plan.lastTileY; tileY++)
        {
            for (var tileX = plan.firstTileX; tileX <= plan.lastTileX; tileX++)
            {
                var currentTileNumber = tileIndex + 1;
                var progressStart = tileIndex / (float)Math.Max(1, plan.totalTiles);
                var url =
                    $"https://tile.googleapis.com/v1/2dtiles/{plan.zoomLevel}/{tileX}/{tileY}" +
                    $"?session={Uri.EscapeDataString(session.session)}" +
                    $"&key={Uri.EscapeDataString(apiKey)}";

                var tempImagePath = Path.Combine(tempRoot, $"tile_{tileY}_{tileX}.{session.imageFormat}");
                var tempMercatorPath = Path.Combine(tempRoot, $"tile_{tileY}_{tileX}_mercator.tif");
                var tempGeoTiffPath = Path.Combine(tempRoot, $"tile_{tileY}_{tileX}.tif");
                var tileWest = TileXToLongitude(tileX, plan.zoomLevel);
                var tileEast = TileXToLongitude(tileX + 1d, plan.zoomLevel);
                var tileNorth = TileYToLatitude(tileY, plan.zoomLevel);
                var tileSouth = TileYToLatitude(tileY + 1d, plan.zoomLevel);

                EditorUtility.DisplayProgressBar(
                    SatDownloadProgressTitle,
                    $"Downloading tile {currentTileNumber} of {plan.totalTiles}...",
                    Mathf.Lerp(0.05f, 0.65f, progressStart));
                DownloadToFile(url, tempImagePath);

                EditorUtility.DisplayProgressBar(
                    SatDownloadProgressTitle,
                    $"Georeferencing tile {currentTileNumber} of {plan.totalTiles}...",
                    Mathf.Lerp(0.1f, 0.8f, progressStart + (0.5f / Math.Max(1, plan.totalTiles))));
                GeoreferenceAndWarpWebMercatorImage(
                    tempImagePath,
                    tempMercatorPath,
                    tempGeoTiffPath,
                    tileWest,
                    tileSouth,
                    tileEast,
                    tileNorth);

                tileGeoTiffs[tileIndex++] = tempGeoTiffPath;
            }
        }

        BuildGeoTiffMosaic(tempRoot, "google_satellite.vrt", tileGeoTiffs, outputGeoTiffPath, west, south, east, north);
    }

    private static void BuildGeoTiffMosaic(
        string tempRoot,
        string vrtFileName,
        string[] tileGeoTiffs,
        string outputGeoTiffPath,
        double west,
        double south,
        double east,
        double north)
    {
        var vrtPath = Path.Combine(tempRoot, vrtFileName);
        var buildVrtPath = ResolveQgisExecutable("gdalbuildvrt.exe");
        var gdalWarpPath = ResolveQgisExecutable("gdalwarp.exe");
        EditorUtility.DisplayProgressBar(SatDownloadProgressTitle, "Building satellite mosaic...", 0.9f);
        var vrtArgs = $"{Quote(vrtPath)} {string.Join(" ", tileGeoTiffs.Select(Quote))}";
        RunProcess(buildVrtPath, vrtArgs);

        EditorUtility.DisplayProgressBar(SatDownloadProgressTitle, "Writing final satellite GeoTIFF...", 0.97f);
        var warpArgs = string.Join(" ", new[]
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
            "-of GTiff",
            Quote(vrtPath),
            Quote(outputGeoTiffPath)
        });
        RunProcess(gdalWarpPath, warpArgs);
    }

    private static int ResolveProviderMaxTileSize(string providerId)
    {
        switch (providerId)
        {
            case TerrainDataProviderIds.Mapbox:
                return MapboxMaxStaticImageSize;
            case TerrainDataProviderIds.GoogleMapsPlatform:
                return GoogleMapsDefaultTileSize;
            default:
                return MapboxMaxStaticImageSize;
        }
    }

    private static double ResolvePixelsPerMeter(TerrainForgerSatelliteResolutionUnit unit, float resolutionValue)
    {
        var safeValue = Math.Max(0.000001d, resolutionValue);
        return unit == TerrainForgerSatelliteResolutionUnit.PixelsPerMeter
            ? safeValue
            : 1d / safeValue;
    }

    private static string ResolveProviderDisplayName(string providerId)
    {
        var providers = TerrainDataServiceSettings.GetBuiltInProviders();
        for (var i = 0; i < providers.Count; i++)
        {
            if (providers[i].providerId == providerId)
            {
                return providers[i].displayName;
            }
        }

        return providerId;
    }

    private static void DeletePreviousSatelliteImages(string lastSatelliteImagePath)
    {
        if (string.IsNullOrWhiteSpace(lastSatelliteImagePath))
        {
            return;
        }

        var absolutePath = ToAbsoluteProjectPath(lastSatelliteImagePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
            var metaPath = absolutePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }
    }

    private static double LongitudeToWebMercatorX(double longitude)
    {
        const double earthRadius = 6378137d;
        return earthRadius * longitude * Mathf.Deg2Rad;
    }

    private static double LatitudeToWebMercatorY(double latitude)
    {
        const double earthRadius = 6378137d;
        var clampedLatitude = Mathf.Clamp((float)latitude, -85.05112878f, 85.05112878f);
        var radians = clampedLatitude * Mathf.Deg2Rad;
        return earthRadius * Math.Log(Math.Tan((Math.PI * 0.25d) + (radians * 0.5d)));
    }

    private static double WebMercatorXToLongitude(double x)
    {
        const double earthRadius = 6378137d;
        return (x / earthRadius) * Mathf.Rad2Deg;
    }

    private static double WebMercatorYToLatitude(double y)
    {
        const double earthRadius = 6378137d;
        return (2d * Math.Atan(Math.Exp(y / earthRadius)) - (Math.PI * 0.5d)) * Mathf.Rad2Deg;
    }

    private static string ResolveGoogleMapsLanguage()
    {
        var cultureName = CultureInfo.CurrentUICulture?.Name;
        return string.IsNullOrWhiteSpace(cultureName) ? "en-US" : cultureName;
    }

    private static string ResolveGoogleMapsRegion()
    {
        try
        {
            var regionName = RegionInfo.CurrentRegion?.TwoLetterISORegionName;
            if (!string.IsNullOrWhiteSpace(regionName))
            {
                return regionName.ToUpperInvariant();
            }
        }
        catch
        {
        }

        return "US";
    }

    private static int ResolveGoogleZoomLevel(double desiredMetersPerPixel, double latitude)
    {
        var safeMetersPerPixel = Math.Max(0.000001d, desiredMetersPerPixel);
        var baseResolution = ResolveGoogleMetersPerPixel(latitude, 0);
        var zoom = (int)Math.Ceiling(Math.Log(baseResolution / safeMetersPerPixel, 2d));
        return Mathf.Clamp(zoom, 0, GoogleMapsMaxZoomLevel);
    }

    private static double ResolveGoogleMetersPerPixel(double latitude, int zoomLevel)
    {
        var latitudeRadians = Mathf.Deg2Rad * Mathf.Clamp((float)latitude, -85.05112878f, 85.05112878f);
        return 156543.03392804097d * Math.Cos(latitudeRadians) / Math.Pow(2d, zoomLevel);
    }

    private static double LongitudeToTileX(double longitude, int zoomLevel)
    {
        var tileCount = Math.Pow(2d, zoomLevel);
        return ((longitude + 180d) / 360d) * tileCount;
    }

    private static double LatitudeToTileY(double latitude, int zoomLevel)
    {
        var clampedLatitude = Mathf.Clamp((float)latitude, -85.05112878f, 85.05112878f) * Mathf.Deg2Rad;
        var tileCount = Math.Pow(2d, zoomLevel);
        var mercator = Math.Log(Math.Tan(clampedLatitude) + (1d / Math.Cos(clampedLatitude)));
        return (1d - (mercator / Math.PI)) * 0.5d * tileCount;
    }

    private static double TileXToLongitude(double tileX, int zoomLevel)
    {
        var tileCount = Math.Pow(2d, zoomLevel);
        return (tileX / tileCount) * 360d - 180d;
    }

    private static double TileYToLatitude(double tileY, int zoomLevel)
    {
        var tileCount = Math.Pow(2d, zoomLevel);
        var mercator = Math.PI * (1d - (2d * tileY / tileCount));
        return Mathf.Rad2Deg * (float)Math.Atan(Math.Sinh(mercator));
    }
}
