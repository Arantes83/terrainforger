using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public enum TerrainForgerVectorSourceType
{
    LocalVectorFile,
    OpenStreetMapOverpass
}

public enum TerrainForgerVectorLayerSelectionMode
{
    Auto,
    Buildings,
    Roads
}

public enum VectorLayerKind
{
    Unknown,
    Building,
    Road,
    Water,
    Waterway,
    Landuse,
    Admin
}

public enum VectorGeometryType
{
    Unknown,
    Point,
    LineString,
    MultiLineString,
    Polygon,
    MultiPolygon
}

[Serializable]
public struct GeoCoordinate
{
    public double latitude;
    public double longitude;

    public GeoCoordinate(double latitude, double longitude)
    {
        this.latitude = latitude;
        this.longitude = longitude;
    }
}

[Serializable]
public struct GeoBounds
{
    public double north;
    public double south;
    public double west;
    public double east;

    public GeoBounds(double north, double south, double west, double east)
    {
        this.north = north;
        this.south = south;
        this.west = west;
        this.east = east;
    }

    public bool IsValid
    {
        get { return north > south && east > west; }
    }

    public static GeoBounds FromWorkflowSettings(TerrainForgeWorkflowSettings settings)
    {
        return new GeoBounds(
            settings.northBound.ToDecimalDegrees(),
            settings.southBound.ToDecimalDegrees(),
            settings.westBound.ToDecimalDegrees(),
            settings.eastBound.ToDecimalDegrees());
    }
}

public struct TerrainForgerVector2d
{
    public double x;
    public double y;

    public TerrainForgerVector2d(double x, double y)
    {
        this.x = x;
        this.y = y;
    }
}

public sealed class VectorDataset
{
    public GeoBounds Bounds;
    public string SourceName = string.Empty;
    public List<VectorLayer> Layers = new List<VectorLayer>();
}

public sealed class VectorLayer
{
    public string Name = string.Empty;
    public VectorLayerKind Kind = VectorLayerKind.Unknown;
    public List<VectorFeature> Features = new List<VectorFeature>();
}

public sealed class VectorFeature
{
    public string SourceId = string.Empty;
    public VectorLayerKind Kind = VectorLayerKind.Unknown;
    public VectorGeometry Geometry = new VectorGeometry();
    public Dictionary<string, object> Properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
}

public sealed class VectorGeometry
{
    public VectorGeometryType Type = VectorGeometryType.Unknown;
    public List<GeoCoordinate> Points = new List<GeoCoordinate>();
    public List<List<GeoCoordinate>> LineStrings = new List<List<GeoCoordinate>>();
    public List<List<GeoCoordinate>> PolygonRings = new List<List<GeoCoordinate>>();
    public List<List<List<GeoCoordinate>>> MultiPolygonRings = new List<List<List<GeoCoordinate>>>();
}

[Serializable]
public sealed class TerrainForgerVectorImportReport
{
    public string source = string.Empty;
    public GeoBounds bounds;
    public int buildingCount;
    public int roadCount;
    public int skippedInvalidGeometry;
    public int missingHeightCount;
    public int usedFallbackHeightCount;
    public string generationDateUtc = string.Empty;
}

[FilePath("UserSettings/TerrainForgerVectorImportSettings.asset", FilePathAttribute.Location.ProjectFolder)]
public class TerrainForgerVectorImportSettings : ScriptableSingleton<TerrainForgerVectorImportSettings>
{
    [Header("Source")]
    public TerrainForgerVectorSourceType sourceType = TerrainForgerVectorSourceType.OpenStreetMapOverpass;
    public string localVectorFilePath = string.Empty;
    public string localLayerName = string.Empty;
    public TerrainForgerVectorLayerSelectionMode localLayerMode = TerrainForgerVectorLayerSelectionMode.Auto;
    public string overpassEndpoint = "https://overpass-api.de/api/interpreter";

    [Header("Bounds")]
    public bool useTerrainForgerBounds = true;
    public LatitudeDdm manualNorthBound = LatitudeDdm.Create(LatitudeHemisphere.South, 12, 30, 0);
    public LatitudeDdm manualSouthBound = LatitudeDdm.Create(LatitudeHemisphere.South, 13, 15, 0);
    public LongitudeDdm manualWestBound = LongitudeDdm.Create(LongitudeHemisphere.West, 39, 0, 0);
    public LongitudeDdm manualEastBound = LongitudeDdm.Create(LongitudeHemisphere.West, 38, 20, 0);

    [Header("Layers")]
    public bool importBuildings = true;
    public bool importRoads = true;

    [Header("Attribute Mapping")]
    public string heightField = string.Empty;
    public string levelsField = string.Empty;
    public string buildingTypeField = string.Empty;
    public string roadClassField = string.Empty;
    public string widthField = string.Empty;

    [Header("Generation")]
    public float defaultBuildingHeight = 6f;
    public float floorHeight = 3f;
    public float minimumBuildingHeight = 2.5f;
    public float maximumBuildingHeight = 120f;
    public bool generateBuildingColliders;
    public float defaultRoadWidth = 7f;
    public float laneWidth = 3.5f;
    public float roadElevationOffset = 0.05f;
    public bool generateRoadMeshes = true;
    public bool generateUnityObjects = true;

    [Header("Output")]
    public string outputFolder = "Assets/Generated/TerrainForger";
    public bool saveNormalizedGeoJson = true;

    public GeoBounds ResolveBounds()
    {
        if (useTerrainForgerBounds)
        {
            return GeoBounds.FromWorkflowSettings(TerrainForgeWorkflowSettings.instance);
        }

        return new GeoBounds(
            manualNorthBound.ToDecimalDegrees(),
            manualSouthBound.ToDecimalDegrees(),
            manualWestBound.ToDecimalDegrees(),
            manualEastBound.ToDecimalDegrees());
    }

    public void SaveSettings()
    {
        Save(true);
    }
}

public sealed class TerrainForgerVectorImportContext
{
    public GeoBounds Bounds;
    public Action<string> Log;

    public void AddLog(string message)
    {
        if (Log != null)
        {
            Log(message);
        }
    }
}

public interface IVectorDataProvider
{
    string DisplayName { get; }
    VectorDataset Import(TerrainForgerVectorImportSettings settings, TerrainForgerVectorImportContext context);
}

public sealed class GeoToUnityTransform
{
    private const double EarthRadiusMeters = 6378137d;

    public GeoBounds Bounds;

    private readonly Terrain terrain;
    private readonly double originX;
    private readonly double originZ;
    private readonly double widthMeters;
    private readonly double depthMeters;
    private readonly double terrainWidth;
    private readonly double terrainDepth;
    private readonly Vector3 terrainOrigin;

    public GeoToUnityTransform(GeoBounds bounds, Terrain terrain)
    {
        Bounds = bounds;
        this.terrain = terrain;
        originX = LongitudeToMeters(bounds.west, bounds.west, GetCenterLatitude(bounds));
        originZ = LatitudeToMeters(bounds.south, bounds.south);
        widthMeters = Math.Max(0.001d, LongitudeToMeters(bounds.east, bounds.west, GetCenterLatitude(bounds)));
        depthMeters = Math.Max(0.001d, LatitudeToMeters(bounds.north, bounds.south));

        if (terrain != null && terrain.terrainData != null)
        {
            terrainOrigin = terrain.transform.position;
            terrainWidth = Math.Max(0.001d, terrain.terrainData.size.x);
            terrainDepth = Math.Max(0.001d, terrain.terrainData.size.z);
        }
        else
        {
            terrainOrigin = Vector3.zero;
            terrainWidth = widthMeters;
            terrainDepth = depthMeters;
        }
    }

    public TerrainForgerVector2d Project(GeoCoordinate coordinate)
    {
        var centerLatitude = GetCenterLatitude(Bounds);
        var x = LongitudeToMeters(coordinate.longitude, Bounds.west, centerLatitude) - originX;
        var z = LatitudeToMeters(coordinate.latitude, Bounds.south) - originZ;
        return new TerrainForgerVector2d(x, z);
    }

    public Vector3 ToUnityPosition(GeoCoordinate coordinate, Terrain targetTerrain)
    {
        var projected = Project(coordinate);
        var normalizedX = projected.x / widthMeters;
        var normalizedZ = projected.y / depthMeters;
        var x = terrainOrigin.x + (float)(normalizedX * terrainWidth);
        var z = terrainOrigin.z + (float)(normalizedZ * terrainDepth);
        var position = new Vector3(x, terrainOrigin.y, z);
        position.y = SampleHeight(position, targetTerrain);
        return position;
    }

    public Vector3 ToUnityPosition(GeoCoordinate coordinate)
    {
        return ToUnityPosition(coordinate, terrain);
    }

    public float SampleHeight(Vector3 worldPosition, Terrain targetTerrain)
    {
        if (targetTerrain == null)
        {
            return worldPosition.y;
        }

        return targetTerrain.SampleHeight(worldPosition) + targetTerrain.transform.position.y;
    }

    private static double GetCenterLatitude(GeoBounds bounds)
    {
        return (bounds.north + bounds.south) * 0.5d;
    }

    private static double LongitudeToMeters(double longitude, double originLongitude, double referenceLatitude)
    {
        var deltaLongitudeRadians = DegreesToRadians(longitude - originLongitude);
        var latitudeRadians = DegreesToRadians(referenceLatitude);
        return EarthRadiusMeters * deltaLongitudeRadians * Math.Cos(latitudeRadians);
    }

    private static double LatitudeToMeters(double latitude, double originLatitude)
    {
        return EarthRadiusMeters * DegreesToRadians(latitude - originLatitude);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * (Math.PI / 180d);
    }
}

public static class TerrainForgerVectorExternalTools
{
    private const int ExternalProcessTimeoutMilliseconds = 30 * 60 * 1000;

    public static string ResolveQgisExecutable(string executableName)
    {
        var qgisInstallFolder = TerrainDataServiceSettings.instance.QgisInstallFolder;
        if (string.IsNullOrWhiteSpace(qgisInstallFolder))
        {
            throw new InvalidOperationException($"QGIS install folder is required to use {executableName}. Set it in TerrainForger Data Services.");
        }

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

    public static string RunProcess(string executable, string arguments, string toolLabel)
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

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using (var outputWaitHandle = new AutoResetEvent(false))
            using (var errorWaitHandle = new AutoResetEvent(false))
            {
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data == null)
                    {
                        outputWaitHandle.Set();
                    }
                    else
                    {
                        stdout.AppendLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data == null)
                    {
                        errorWaitHandle.Set();
                    }
                    else
                    {
                        stderr.AppendLine(args.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(ExternalProcessTimeoutMilliseconds))
                {
                    TryKillProcess(process);
                    throw new TimeoutException(
                        $"{toolLabel} timed out after {ExternalProcessTimeoutMilliseconds / 1000} seconds.\nCommand: {executable} {arguments}");
                }

                outputWaitHandle.WaitOne(5000);
                errorWaitHandle.WaitOne(5000);
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{toolLabel} failed with exit code {process.ExitCode}.\nCommand: {executable} {arguments}\n{stdout}\n{stderr}");
            }

            return stdout.ToString();
        }
    }

    public static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    public static string ToAssetPath(string absolutePath)
    {
        var normalizedFullPath = Path.GetFullPath(absolutePath).Replace('\\', '/');
        var normalizedAssetsPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');

        if (normalizedFullPath.StartsWith(normalizedAssetsPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets" + normalizedFullPath.Substring(normalizedAssetsPath.Length);
        }

        return normalizedFullPath;
    }

    public static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    public static string FormatDouble(double value)
    {
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    public static void EnsureAssetFolder(string assetFolder)
    {
        var normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        var parts = normalized.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (process != null && !process.HasExited)
            {
                process.Kill();
            }
        }
        catch
        {
        }
    }
}

public static class TerrainForgerGeoJsonUtility
{
    public static VectorDataset ReadDataset(string geoJson, string sourceName, TerrainForgerVectorLayerSelectionMode layerMode)
    {
        var root = TerrainForgerSimpleJson.ParseObject(geoJson);
        var dataset = new VectorDataset
        {
            SourceName = sourceName
        };

        object featuresValue;
        if (!root.TryGetValue("features", out featuresValue))
        {
            throw new InvalidOperationException("GeoJSON FeatureCollection does not contain a features array.");
        }

        var features = featuresValue as List<object>;
        if (features == null)
        {
            throw new InvalidOperationException("GeoJSON features value is not an array.");
        }

        var buildingLayer = new VectorLayer { Name = "buildings", Kind = VectorLayerKind.Building };
        var roadLayer = new VectorLayer { Name = "roads", Kind = VectorLayerKind.Road };
        var unknownLayer = new VectorLayer { Name = "unknown", Kind = VectorLayerKind.Unknown };

        for (var i = 0; i < features.Count; i++)
        {
            var featureObject = features[i] as Dictionary<string, object>;
            if (featureObject == null)
            {
                continue;
            }

            var feature = ReadFeature(featureObject, layerMode, i);
            if (feature == null)
            {
                continue;
            }

            if (feature.Kind == VectorLayerKind.Building)
            {
                buildingLayer.Features.Add(feature);
            }
            else if (feature.Kind == VectorLayerKind.Road)
            {
                roadLayer.Features.Add(feature);
            }
            else
            {
                unknownLayer.Features.Add(feature);
            }
        }

        if (buildingLayer.Features.Count > 0)
        {
            dataset.Layers.Add(buildingLayer);
        }

        if (roadLayer.Features.Count > 0)
        {
            dataset.Layers.Add(roadLayer);
        }

        if (unknownLayer.Features.Count > 0)
        {
            dataset.Layers.Add(unknownLayer);
        }

        dataset.Bounds = CalculateBounds(dataset);
        return dataset;
    }

    public static string WriteFeatureCollection(VectorDataset dataset, VectorLayerKind kind)
    {
        var root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var features = new List<object>();
        root["type"] = "FeatureCollection";
        root["features"] = features;

        for (var i = 0; i < dataset.Layers.Count; i++)
        {
            var layer = dataset.Layers[i];
            if (layer.Kind != kind)
            {
                continue;
            }

            for (var f = 0; f < layer.Features.Count; f++)
            {
                features.Add(WriteFeature(layer.Features[f]));
            }
        }

        return TerrainForgerSimpleJson.Stringify(root, true);
    }

    public static int CountFeatures(VectorDataset dataset, VectorLayerKind kind)
    {
        var count = 0;
        for (var i = 0; i < dataset.Layers.Count; i++)
        {
            if (dataset.Layers[i].Kind == kind)
            {
                count += dataset.Layers[i].Features.Count;
            }
        }

        return count;
    }

    public static IEnumerable<VectorFeature> EnumerateFeatures(VectorDataset dataset, VectorLayerKind kind)
    {
        for (var i = 0; i < dataset.Layers.Count; i++)
        {
            var layer = dataset.Layers[i];
            if (layer.Kind != kind)
            {
                continue;
            }

            for (var f = 0; f < layer.Features.Count; f++)
            {
                yield return layer.Features[f];
            }
        }
    }

    private static VectorFeature ReadFeature(Dictionary<string, object> featureObject, TerrainForgerVectorLayerSelectionMode layerMode, int index)
    {
        object geometryValue;
        if (!featureObject.TryGetValue("geometry", out geometryValue) || geometryValue == null)
        {
            return null;
        }

        var geometryObject = geometryValue as Dictionary<string, object>;
        if (geometryObject == null)
        {
            return null;
        }

        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        object propertiesValue;
        if (featureObject.TryGetValue("properties", out propertiesValue))
        {
            var propertyObject = propertiesValue as Dictionary<string, object>;
            if (propertyObject != null)
            {
                foreach (var pair in propertyObject)
                {
                    properties[pair.Key] = pair.Value;
                }
            }
        }

        var geometry = ReadGeometry(geometryObject);
        var kind = ResolveLayerKind(properties, geometry, layerMode);
        var feature = new VectorFeature
        {
            SourceId = ResolveSourceId(featureObject, properties, index),
            Kind = kind,
            Geometry = geometry,
            Properties = properties
        };
        return feature;
    }

    private static string ResolveSourceId(Dictionary<string, object> featureObject, Dictionary<string, object> properties, int index)
    {
        object idValue;
        if (featureObject.TryGetValue("id", out idValue) && idValue != null)
        {
            return Convert.ToString(idValue, CultureInfo.InvariantCulture);
        }

        if (properties.TryGetValue("id", out idValue) && idValue != null)
        {
            return Convert.ToString(idValue, CultureInfo.InvariantCulture);
        }

        if (properties.TryGetValue("@id", out idValue) && idValue != null)
        {
            return Convert.ToString(idValue, CultureInfo.InvariantCulture);
        }

        return "feature_" + index.ToString(CultureInfo.InvariantCulture);
    }

    private static VectorLayerKind ResolveLayerKind(
        Dictionary<string, object> properties,
        VectorGeometry geometry,
        TerrainForgerVectorLayerSelectionMode layerMode)
    {
        if (layerMode == TerrainForgerVectorLayerSelectionMode.Buildings)
        {
            return VectorLayerKind.Building;
        }

        if (layerMode == TerrainForgerVectorLayerSelectionMode.Roads)
        {
            return VectorLayerKind.Road;
        }

        if (properties.ContainsKey("building") ||
            properties.ContainsKey("building:levels") ||
            properties.ContainsKey("height") ||
            properties.ContainsKey("render_height"))
        {
            return VectorLayerKind.Building;
        }

        if (properties.ContainsKey("highway") ||
            properties.ContainsKey("road") ||
            properties.ContainsKey("class") ||
            properties.ContainsKey("road_class"))
        {
            return VectorLayerKind.Road;
        }

        if (geometry.Type == VectorGeometryType.LineString || geometry.Type == VectorGeometryType.MultiLineString)
        {
            return VectorLayerKind.Road;
        }

        if (geometry.Type == VectorGeometryType.Polygon || geometry.Type == VectorGeometryType.MultiPolygon)
        {
            return VectorLayerKind.Building;
        }

        return VectorLayerKind.Unknown;
    }

    private static VectorGeometry ReadGeometry(Dictionary<string, object> geometryObject)
    {
        var typeText = TerrainForgerSimpleJson.GetString(geometryObject, "type");
        object coordinatesValue;
        if (!geometryObject.TryGetValue("coordinates", out coordinatesValue))
        {
            return new VectorGeometry();
        }

        var geometry = new VectorGeometry();
        if (string.Equals(typeText, "Point", StringComparison.OrdinalIgnoreCase))
        {
            geometry.Type = VectorGeometryType.Point;
            geometry.Points.Add(ReadCoordinate(coordinatesValue));
        }
        else if (string.Equals(typeText, "LineString", StringComparison.OrdinalIgnoreCase))
        {
            geometry.Type = VectorGeometryType.LineString;
            geometry.LineStrings.Add(ReadCoordinateList(coordinatesValue));
        }
        else if (string.Equals(typeText, "MultiLineString", StringComparison.OrdinalIgnoreCase))
        {
            geometry.Type = VectorGeometryType.MultiLineString;
            geometry.LineStrings = ReadLineStringList(coordinatesValue);
        }
        else if (string.Equals(typeText, "Polygon", StringComparison.OrdinalIgnoreCase))
        {
            geometry.Type = VectorGeometryType.Polygon;
            geometry.PolygonRings = ReadLineStringList(coordinatesValue);
        }
        else if (string.Equals(typeText, "MultiPolygon", StringComparison.OrdinalIgnoreCase))
        {
            geometry.Type = VectorGeometryType.MultiPolygon;
            geometry.MultiPolygonRings = ReadPolygonList(coordinatesValue);
        }

        return geometry;
    }

    private static GeoCoordinate ReadCoordinate(object value)
    {
        var array = value as List<object>;
        if (array == null || array.Count < 2)
        {
            return new GeoCoordinate();
        }

        var longitude = Convert.ToDouble(array[0], CultureInfo.InvariantCulture);
        var latitude = Convert.ToDouble(array[1], CultureInfo.InvariantCulture);
        return new GeoCoordinate(latitude, longitude);
    }

    private static List<GeoCoordinate> ReadCoordinateList(object value)
    {
        var result = new List<GeoCoordinate>();
        var array = value as List<object>;
        if (array == null)
        {
            return result;
        }

        for (var i = 0; i < array.Count; i++)
        {
            result.Add(ReadCoordinate(array[i]));
        }

        return result;
    }

    private static List<List<GeoCoordinate>> ReadLineStringList(object value)
    {
        var result = new List<List<GeoCoordinate>>();
        var array = value as List<object>;
        if (array == null)
        {
            return result;
        }

        for (var i = 0; i < array.Count; i++)
        {
            result.Add(ReadCoordinateList(array[i]));
        }

        return result;
    }

    private static List<List<List<GeoCoordinate>>> ReadPolygonList(object value)
    {
        var result = new List<List<List<GeoCoordinate>>>();
        var array = value as List<object>;
        if (array == null)
        {
            return result;
        }

        for (var i = 0; i < array.Count; i++)
        {
            result.Add(ReadLineStringList(array[i]));
        }

        return result;
    }

    private static Dictionary<string, object> WriteFeature(VectorFeature feature)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        result["type"] = "Feature";
        result["id"] = feature.SourceId;
        result["properties"] = new Dictionary<string, object>(feature.Properties, StringComparer.OrdinalIgnoreCase);
        result["geometry"] = WriteGeometry(feature.Geometry);
        return result;
    }

    private static Dictionary<string, object> WriteGeometry(VectorGeometry geometry)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        switch (geometry.Type)
        {
            case VectorGeometryType.Point:
                result["type"] = "Point";
                result["coordinates"] = geometry.Points.Count > 0 ? WriteCoordinate(geometry.Points[0]) : new List<object>();
                break;
            case VectorGeometryType.LineString:
                result["type"] = "LineString";
                result["coordinates"] = geometry.LineStrings.Count > 0 ? WriteCoordinateList(geometry.LineStrings[0]) : new List<object>();
                break;
            case VectorGeometryType.MultiLineString:
                result["type"] = "MultiLineString";
                result["coordinates"] = WriteLineStringList(geometry.LineStrings);
                break;
            case VectorGeometryType.Polygon:
                result["type"] = "Polygon";
                result["coordinates"] = WriteLineStringList(geometry.PolygonRings);
                break;
            case VectorGeometryType.MultiPolygon:
                result["type"] = "MultiPolygon";
                result["coordinates"] = WritePolygonList(geometry.MultiPolygonRings);
                break;
            default:
                result["type"] = "GeometryCollection";
                result["geometries"] = new List<object>();
                break;
        }

        return result;
    }

    private static List<object> WriteCoordinate(GeoCoordinate coordinate)
    {
        return new List<object> { coordinate.longitude, coordinate.latitude };
    }

    private static List<object> WriteCoordinateList(List<GeoCoordinate> coordinates)
    {
        var result = new List<object>();
        for (var i = 0; i < coordinates.Count; i++)
        {
            result.Add(WriteCoordinate(coordinates[i]));
        }

        return result;
    }

    private static List<object> WriteLineStringList(List<List<GeoCoordinate>> lineStrings)
    {
        var result = new List<object>();
        for (var i = 0; i < lineStrings.Count; i++)
        {
            result.Add(WriteCoordinateList(lineStrings[i]));
        }

        return result;
    }

    private static List<object> WritePolygonList(List<List<List<GeoCoordinate>>> polygons)
    {
        var result = new List<object>();
        for (var i = 0; i < polygons.Count; i++)
        {
            result.Add(WriteLineStringList(polygons[i]));
        }

        return result;
    }

    private static GeoBounds CalculateBounds(VectorDataset dataset)
    {
        var hasValue = false;
        var north = double.NegativeInfinity;
        var south = double.PositiveInfinity;
        var west = double.PositiveInfinity;
        var east = double.NegativeInfinity;

        foreach (var feature in EnumerateAllFeatures(dataset))
        {
            foreach (var coordinate in EnumerateCoordinates(feature.Geometry))
            {
                hasValue = true;
                north = Math.Max(north, coordinate.latitude);
                south = Math.Min(south, coordinate.latitude);
                west = Math.Min(west, coordinate.longitude);
                east = Math.Max(east, coordinate.longitude);
            }
        }

        return hasValue ? new GeoBounds(north, south, west, east) : new GeoBounds();
    }

    private static IEnumerable<VectorFeature> EnumerateAllFeatures(VectorDataset dataset)
    {
        for (var i = 0; i < dataset.Layers.Count; i++)
        {
            var layer = dataset.Layers[i];
            for (var f = 0; f < layer.Features.Count; f++)
            {
                yield return layer.Features[f];
            }
        }
    }

    private static IEnumerable<GeoCoordinate> EnumerateCoordinates(VectorGeometry geometry)
    {
        for (var i = 0; i < geometry.Points.Count; i++)
        {
            yield return geometry.Points[i];
        }

        for (var i = 0; i < geometry.LineStrings.Count; i++)
        {
            var line = geometry.LineStrings[i];
            for (var c = 0; c < line.Count; c++)
            {
                yield return line[c];
            }
        }

        for (var i = 0; i < geometry.PolygonRings.Count; i++)
        {
            var ring = geometry.PolygonRings[i];
            for (var c = 0; c < ring.Count; c++)
            {
                yield return ring[c];
            }
        }

        for (var i = 0; i < geometry.MultiPolygonRings.Count; i++)
        {
            var polygon = geometry.MultiPolygonRings[i];
            for (var r = 0; r < polygon.Count; r++)
            {
                var ring = polygon[r];
                for (var c = 0; c < ring.Count; c++)
                {
                    yield return ring[c];
                }
            }
        }
    }
}

public static class TerrainForgerSimpleJson
{
    public static Dictionary<string, object> ParseObject(string json)
    {
        var value = Parse(json);
        var result = value as Dictionary<string, object>;
        if (result == null)
        {
            throw new InvalidOperationException("JSON root is not an object.");
        }

        return result;
    }

    public static object Parse(string json)
    {
        if (json == null)
        {
            throw new ArgumentNullException(nameof(json));
        }

        var parser = new Parser(json);
        return parser.ParseRoot();
    }

    public static string Stringify(object value, bool pretty)
    {
        var builder = new StringBuilder();
        WriteValue(builder, value, pretty, 0);
        return builder.ToString();
    }

    public static string GetString(Dictionary<string, object> dictionary, string key)
    {
        object value;
        if (!dictionary.TryGetValue(key, out value) || value == null)
        {
            return string.Empty;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static void WriteValue(StringBuilder builder, object value, bool pretty, int depth)
    {
        if (value == null)
        {
            builder.Append("null");
            return;
        }

        var text = value as string;
        if (text != null)
        {
            WriteString(builder, text);
            return;
        }

        if (value is bool)
        {
            builder.Append((bool)value ? "true" : "false");
            return;
        }

        if (value is int || value is long || value is float || value is double || value is decimal)
        {
            builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            return;
        }

        var dictionary = value as Dictionary<string, object>;
        if (dictionary != null)
        {
            WriteObject(builder, dictionary, pretty, depth);
            return;
        }

        var list = value as List<object>;
        if (list != null)
        {
            WriteArray(builder, list, pretty, depth);
            return;
        }

        WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private static void WriteObject(StringBuilder builder, Dictionary<string, object> dictionary, bool pretty, int depth)
    {
        builder.Append('{');
        var index = 0;
        foreach (var pair in dictionary)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            WriteNewLineAndIndent(builder, pretty, depth + 1);
            WriteString(builder, pair.Key);
            builder.Append(pretty ? ": " : ":");
            WriteValue(builder, pair.Value, pretty, depth + 1);
            index++;
        }

        if (dictionary.Count > 0)
        {
            WriteNewLineAndIndent(builder, pretty, depth);
        }

        builder.Append('}');
    }

    private static void WriteArray(StringBuilder builder, List<object> list, bool pretty, int depth)
    {
        builder.Append('[');
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            WriteNewLineAndIndent(builder, pretty, depth + 1);
            WriteValue(builder, list[i], pretty, depth + 1);
        }

        if (list.Count > 0)
        {
            WriteNewLineAndIndent(builder, pretty, depth);
        }

        builder.Append(']');
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < 32)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    private static void WriteNewLineAndIndent(StringBuilder builder, bool pretty, int depth)
    {
        if (!pretty)
        {
            return;
        }

        builder.AppendLine();
        for (var i = 0; i < depth; i++)
        {
            builder.Append("  ");
        }
    }

    private sealed class Parser
    {
        private readonly string json;
        private int index;

        public Parser(string json)
        {
            this.json = json;
        }

        public object ParseRoot()
        {
            SkipWhitespace();
            var value = ParseValue();
            SkipWhitespace();
            if (index != json.Length)
            {
                throw new InvalidOperationException("Unexpected trailing JSON content.");
            }

            return value;
        }

        private object ParseValue()
        {
            SkipWhitespace();
            if (index >= json.Length)
            {
                throw new InvalidOperationException("Unexpected end of JSON.");
            }

            var c = json[index];
            if (c == '{')
            {
                return ParseObjectValue();
            }

            if (c == '[')
            {
                return ParseArrayValue();
            }

            if (c == '"')
            {
                return ParseStringValue();
            }

            if (c == 't')
            {
                Expect("true");
                return true;
            }

            if (c == 'f')
            {
                Expect("false");
                return false;
            }

            if (c == 'n')
            {
                Expect("null");
                return null;
            }

            return ParseNumberValue();
        }

        private Dictionary<string, object> ParseObjectValue()
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Expect('{');
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                var key = ParseStringValue();
                SkipWhitespace();
                Expect(':');
                result[key] = ParseValue();
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private List<object> ParseArrayValue()
        {
            var result = new List<object>();
            Expect('[');
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return result;
            }

            while (true)
            {
                result.Add(ParseValue());
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private string ParseStringValue()
        {
            Expect('"');
            var builder = new StringBuilder();
            while (index < json.Length)
            {
                var c = json[index++];
                if (c == '"')
                {
                    return builder.ToString();
                }

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (index >= json.Length)
                {
                    throw new InvalidOperationException("Invalid JSON escape sequence.");
                }

                var escaped = json[index++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escaped);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        if (index + 4 > json.Length)
                        {
                            throw new InvalidOperationException("Invalid JSON unicode escape sequence.");
                        }

                        var hex = json.Substring(index, 4);
                        builder.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported JSON escape sequence: \\" + escaped);
                }
            }

            throw new InvalidOperationException("Unterminated JSON string.");
        }

        private object ParseNumberValue()
        {
            var start = index;
            if (json[index] == '-')
            {
                index++;
            }

            while (index < json.Length && char.IsDigit(json[index]))
            {
                index++;
            }

            var isDouble = false;
            if (index < json.Length && json[index] == '.')
            {
                isDouble = true;
                index++;
                while (index < json.Length && char.IsDigit(json[index]))
                {
                    index++;
                }
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                isDouble = true;
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                {
                    index++;
                }

                while (index < json.Length && char.IsDigit(json[index]))
                {
                    index++;
                }
            }

            var text = json.Substring(start, index - start);
            if (isDouble)
            {
                return double.Parse(text, CultureInfo.InvariantCulture);
            }

            return long.Parse(text, CultureInfo.InvariantCulture);
        }

        private void Expect(string value)
        {
            if (index + value.Length > json.Length ||
                !string.Equals(json.Substring(index, value.Length), value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected JSON token: " + value);
            }

            index += value.Length;
        }

        private void Expect(char value)
        {
            SkipWhitespace();
            if (index >= json.Length || json[index] != value)
            {
                throw new InvalidOperationException("Expected JSON character: " + value);
            }

            index++;
        }

        private bool TryConsume(char value)
        {
            SkipWhitespace();
            if (index < json.Length && json[index] == value)
            {
                index++;
                return true;
            }

            return false;
        }

        private void SkipWhitespace()
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }
    }
}
