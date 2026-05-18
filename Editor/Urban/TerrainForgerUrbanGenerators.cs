using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class TerrainForgerUrbanGenerationResult
{
    public TerrainForgerVectorImportReport Report = new TerrainForgerVectorImportReport();
    public GameObject RootObject;
}

public static class TerrainForgerUrbanGenerator
{
    private const string RootObjectName = "TerrainForger_Urban";

    public static TerrainForgerUrbanGenerationResult Generate(
        VectorDataset dataset,
        TerrainForgerVectorImportSettings settings,
        Terrain targetTerrain,
        Action<string> log)
    {
        return Generate(
            dataset,
            settings,
            targetTerrain,
            log,
            settings.importBuildings,
            settings.importRoads && settings.generateRoadMeshes);
    }

    public static TerrainForgerUrbanGenerationResult Generate(
        VectorDataset dataset,
        TerrainForgerVectorImportSettings settings,
        Terrain targetTerrain,
        Action<string> log,
        bool placeBuildings,
        bool placeRoads)
    {
        if (dataset == null)
        {
            throw new ArgumentNullException(nameof(dataset));
        }

        if (!placeBuildings && !placeRoads)
        {
            throw new InvalidOperationException("Enable at least one vector layer before placing objects.");
        }

        var result = new TerrainForgerUrbanGenerationResult();
        result.Report.source = dataset.SourceName;
        result.Report.bounds = dataset.Bounds;
        result.Report.generationDateUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        var outputRoot = settings.outputFolder.Replace('\\', '/').TrimEnd('/');
        var urbanFolder = outputRoot + "/Urban";
        var buildingFolder = urbanFolder + "/Buildings";
        var roadFolder = urbanFolder + "/Roads";
        TerrainForgerVectorExternalTools.EnsureAssetFolder(outputRoot);
        TerrainForgerVectorExternalTools.EnsureAssetFolder(urbanFolder);

        if (placeBuildings)
        {
            TerrainForgerVectorExternalTools.EnsureAssetFolder(buildingFolder);
        }

        if (placeRoads)
        {
            TerrainForgerVectorExternalTools.EnsureAssetFolder(roadFolder);
        }

        var root = GetOrCreateRootObject();
        result.RootObject = root;
        var buildingsRoot = placeBuildings ? RecreateLayerRoot(root.transform, "Buildings") : null;
        var roadsRoot = placeRoads ? RecreateLayerRoot(root.transform, "Roads") : null;

        var transform = new GeoToUnityTransform(dataset.Bounds.IsValid ? dataset.Bounds : settings.ResolveBounds(), targetTerrain);
        var buildingMaterial = placeBuildings
            ? TerrainForgerUrbanAssetWriter.GetOrCreateMaterial(
                urbanFolder + "/TerrainForger_Buildings.mat",
                new Color(0.72f, 0.72f, 0.68f, 1f))
            : null;
        var roadMaterial = placeRoads
            ? TerrainForgerUrbanAssetWriter.GetOrCreateMaterial(
                urbanFolder + "/TerrainForger_Roads.mat",
                new Color(0.08f, 0.08f, 0.08f, 1f))
            : null;

        var totalFeatures = (placeBuildings ? TerrainForgerGeoJsonUtility.CountFeatures(dataset, VectorLayerKind.Building) : 0) +
            (placeRoads ? TerrainForgerGeoJsonUtility.CountFeatures(dataset, VectorLayerKind.Road) : 0);
        var processedFeatures = 0;

        try
        {
            if (placeBuildings)
            {
                foreach (var feature in TerrainForgerGeoJsonUtility.EnumerateFeatures(dataset, VectorLayerKind.Building))
                {
                    if (DisplayPlacementProgress("Placing buildings", processedFeatures, totalFeatures))
                    {
                        throw new OperationCanceledException("Building placement canceled.");
                    }

                    GenerateBuilding(feature, settings, targetTerrain, transform, buildingsRoot.transform, buildingFolder, buildingMaterial, result.Report, log);
                    processedFeatures++;
                }
            }

            if (placeRoads)
            {
                foreach (var feature in TerrainForgerGeoJsonUtility.EnumerateFeatures(dataset, VectorLayerKind.Road))
                {
                    if (DisplayPlacementProgress("Placing roads", processedFeatures, totalFeatures))
                    {
                        throw new OperationCanceledException("Road placement canceled.");
                    }

                    GenerateRoad(feature, settings, targetTerrain, transform, roadsRoot.transform, roadFolder, roadMaterial, result.Report, log);
                    processedFeatures++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return result;
    }

    private static GameObject GetOrCreateRootObject()
    {
        var root = GameObject.Find(RootObjectName);
        return root != null ? root : new GameObject(RootObjectName);
    }

    private static Transform RecreateLayerRoot(Transform parent, string layerName)
    {
        var existing = parent.Find(layerName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        var layerRoot = new GameObject(layerName);
        layerRoot.transform.SetParent(parent, false);
        return layerRoot.transform;
    }

    private static bool DisplayPlacementProgress(string message, int processedFeatures, int totalFeatures)
    {
        if (totalFeatures <= 0)
        {
            return false;
        }

        return EditorUtility.DisplayCancelableProgressBar(
            "TerrainForger Vector Placement",
            message + " " + processedFeatures.ToString(CultureInfo.InvariantCulture) + "/" + totalFeatures.ToString(CultureInfo.InvariantCulture),
            Mathf.Clamp01(processedFeatures / (float)totalFeatures));
    }

    private static void GenerateBuilding(
        VectorFeature feature,
        TerrainForgerVectorImportSettings settings,
        Terrain targetTerrain,
        GeoToUnityTransform transform,
        Transform parent,
        string assetFolder,
        Material material,
        TerrainForgerVectorImportReport report,
        Action<string> log)
    {
        bool usedFallback;
        bool missingHeight;
        var height = BuildingHeightRules.ResolveHeight(feature.Properties, settings, out usedFallback, out missingHeight);
        if (usedFallback)
        {
            report.usedFallbackHeightCount++;
        }

        if (missingHeight)
        {
            report.missingHeightCount++;
        }

        var footprints = GetBuildingFootprints(feature.Geometry);
        for (var i = 0; i < footprints.Count; i++)
        {
            var mesh = BuildingGenerator.BuildMesh(footprints[i], height, targetTerrain, transform);
            if (mesh == null)
            {
                report.skippedInvalidGeometry++;
                continue;
            }

            var safeName = TerrainForgerUrbanAssetWriter.MakeSafeName("Building_" + feature.SourceId + "_" + i.ToString(CultureInfo.InvariantCulture));
            var assetPath = TerrainForgerUrbanAssetWriter.CreateMeshAsset(mesh, assetFolder, safeName);
            var meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            var go = new GameObject(safeName);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = meshAsset;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            if (settings.generateBuildingColliders)
            {
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = meshAsset;
            }

            report.buildingCount++;
        }
    }

    private static void GenerateRoad(
        VectorFeature feature,
        TerrainForgerVectorImportSettings settings,
        Terrain targetTerrain,
        GeoToUnityTransform transform,
        Transform parent,
        string assetFolder,
        Material material,
        TerrainForgerVectorImportReport report,
        Action<string> log)
    {
        var width = RoadWidthRules.ResolveWidth(feature.Properties, settings);
        var lines = GetRoadLines(feature.Geometry);
        for (var i = 0; i < lines.Count; i++)
        {
            var mesh = RoadGenerator.BuildMesh(lines[i], width, targetTerrain, transform, settings.roadElevationOffset);
            if (mesh == null)
            {
                report.skippedInvalidGeometry++;
                continue;
            }

            var safeName = TerrainForgerUrbanAssetWriter.MakeSafeName("Road_" + feature.SourceId + "_" + i.ToString(CultureInfo.InvariantCulture));
            var assetPath = TerrainForgerUrbanAssetWriter.CreateMeshAsset(mesh, assetFolder, safeName);
            var meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            var go = new GameObject(safeName);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = meshAsset;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            report.roadCount++;
        }
    }

    private static List<List<GeoCoordinate>> GetBuildingFootprints(VectorGeometry geometry)
    {
        var result = new List<List<GeoCoordinate>>();
        if (geometry.Type == VectorGeometryType.Polygon)
        {
            if (geometry.PolygonRings.Count > 0)
            {
                result.Add(geometry.PolygonRings[0]);
            }
        }
        else if (geometry.Type == VectorGeometryType.MultiPolygon)
        {
            for (var i = 0; i < geometry.MultiPolygonRings.Count; i++)
            {
                var polygon = geometry.MultiPolygonRings[i];
                if (polygon.Count > 0)
                {
                    result.Add(polygon[0]);
                }
            }
        }

        return result;
    }

    private static List<List<GeoCoordinate>> GetRoadLines(VectorGeometry geometry)
    {
        var result = new List<List<GeoCoordinate>>();
        if (geometry.Type == VectorGeometryType.LineString || geometry.Type == VectorGeometryType.MultiLineString)
        {
            result.AddRange(geometry.LineStrings);
        }
        else if (geometry.Type == VectorGeometryType.Polygon)
        {
            if (geometry.PolygonRings.Count > 0)
            {
                result.Add(geometry.PolygonRings[0]);
            }
        }
        else if (geometry.Type == VectorGeometryType.MultiPolygon)
        {
            for (var i = 0; i < geometry.MultiPolygonRings.Count; i++)
            {
                var polygon = geometry.MultiPolygonRings[i];
                if (polygon.Count > 0)
                {
                    result.Add(polygon[0]);
                }
            }
        }

        return result;
    }
}

public static class BuildingHeightRules
{
    public static float ResolveHeight(
        Dictionary<string, object> properties,
        TerrainForgerVectorImportSettings settings,
        out bool usedFallback,
        out bool missingHeight)
    {
        usedFallback = false;
        missingHeight = false;

        float height;
        if (TryGetFloat(properties, settings.heightField, out height) ||
            TryGetFloat(properties, "height", out height) ||
            TryGetFloat(properties, "render_height", out height))
        {
            return ClampHeight(height, settings);
        }

        float levels;
        if (TryGetFloat(properties, settings.levelsField, out levels) ||
            TryGetFloat(properties, "building:levels", out levels) ||
            TryGetFloat(properties, "levels", out levels))
        {
            return ClampHeight(levels * Mathf.Max(0.01f, settings.floorHeight), settings);
        }

        missingHeight = true;
        usedFallback = true;
        var fallback = ResolveFallbackByType(properties, settings);
        return ClampHeight(fallback, settings);
    }

    private static float ResolveFallbackByType(Dictionary<string, object> properties, TerrainForgerVectorImportSettings settings)
    {
        var buildingType = GetString(properties, settings.buildingTypeField);
        if (string.IsNullOrWhiteSpace(buildingType))
        {
            buildingType = GetString(properties, "building");
        }

        if (string.Equals(buildingType, "garage", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(buildingType, "garages", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(buildingType, "shed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(buildingType, "hut", StringComparison.OrdinalIgnoreCase))
        {
            return Mathf.Max(settings.minimumBuildingHeight, 3f);
        }

        return Mathf.Max(settings.minimumBuildingHeight, settings.defaultBuildingHeight);
    }

    private static float ClampHeight(float height, TerrainForgerVectorImportSettings settings)
    {
        var min = Mathf.Max(0.01f, settings.minimumBuildingHeight);
        var max = Mathf.Max(min, settings.maximumBuildingHeight);
        return Mathf.Clamp(height, min, max);
    }

    private static bool TryGetFloat(Dictionary<string, object> properties, string key, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        object raw;
        if (!properties.TryGetValue(key, out raw) || raw == null)
        {
            return false;
        }

        return TryParseFlexibleFloat(raw, out value);
    }

    private static string GetString(Dictionary<string, object> properties, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        object value;
        if (!properties.TryGetValue(key, out value) || value == null)
        {
            return string.Empty;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    public static bool TryParseFlexibleFloat(object raw, out float value)
    {
        value = 0f;
        if (raw == null)
        {
            return false;
        }

        if (raw is int || raw is long || raw is float || raw is double || raw is decimal)
        {
            value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        var text = Convert.ToString(raw, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Regex.Match(text, @"-?\d+(?:[\.,]\d+)?");
        if (!match.Success)
        {
            return false;
        }

        text = match.Value.Replace(',', '.');
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

public static class RoadWidthRules
{
    public static float ResolveWidth(Dictionary<string, object> properties, TerrainForgerVectorImportSettings settings)
    {
        float width;
        if (TryGetFloat(properties, settings.widthField, out width) ||
            TryGetFloat(properties, "width", out width))
        {
            return Mathf.Max(0.5f, width);
        }

        float lanes;
        if (TryGetFloat(properties, "lanes", out lanes))
        {
            return Mathf.Max(0.5f, lanes * Mathf.Max(0.5f, settings.laneWidth));
        }

        var roadClass = GetRoadClass(properties, settings);
        switch (roadClass)
        {
            case "motorway":
                return 24f;
            case "trunk":
                return 18f;
            case "primary":
                return 14f;
            case "secondary":
                return 12f;
            case "tertiary":
                return 10f;
            case "service":
                return 4f;
            case "living_street":
                return 5f;
            case "pedestrian":
                return 4f;
            case "footway":
            case "path":
            case "steps":
            case "cycleway":
                return 2f;
            case "track":
                return 3f;
            case "residential":
            case "unclassified":
                return 7f;
            default:
                return Mathf.Max(0.5f, settings.defaultRoadWidth);
        }
    }

    private static bool TryGetFloat(Dictionary<string, object> properties, string key, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        object raw;
        return properties.TryGetValue(key, out raw) && BuildingHeightRules.TryParseFlexibleFloat(raw, out value);
    }

    private static string GetRoadClass(Dictionary<string, object> properties, TerrainForgerVectorImportSettings settings)
    {
        var value = GetString(properties, settings.roadClassField);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = GetString(properties, "highway");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            value = GetString(properties, "class");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            value = GetString(properties, "road_class");
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string GetString(Dictionary<string, object> properties, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        object value;
        if (!properties.TryGetValue(key, out value) || value == null)
        {
            return string.Empty;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}

public static class BuildingGenerator
{
    public static Mesh BuildMesh(List<GeoCoordinate> footprint, float height, Terrain terrain, GeoToUnityTransform transform)
    {
        var ring = SanitizeRing(footprint);
        if (ring.Count < 3)
        {
            return null;
        }

        var basePositions = new List<Vector3>();
        for (var i = 0; i < ring.Count; i++)
        {
            basePositions.Add(transform.ToUnityPosition(ring[i], terrain));
        }

        var centroid = CalculateCentroid(basePositions);
        var baseY = transform.SampleHeight(centroid, terrain);
        for (var i = 0; i < basePositions.Count; i++)
        {
            var p = basePositions[i];
            p.y = baseY;
            basePositions[i] = p;
        }

        if (SignedArea(basePositions) < 0f)
        {
            basePositions.Reverse();
        }

        var triangles2d = Triangulate(basePositions);
        if (triangles2d.Count < 3)
        {
            return null;
        }

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();

        for (var i = 0; i < basePositions.Count; i++)
        {
            vertices.Add(basePositions[i]);
            uvs.Add(Vector2.zero);
        }

        var topOffset = vertices.Count;
        for (var i = 0; i < basePositions.Count; i++)
        {
            var top = basePositions[i];
            top.y += height;
            vertices.Add(top);
            uvs.Add(Vector2.one);
        }

        for (var i = 0; i < triangles2d.Count; i += 3)
        {
            triangles.Add(topOffset + triangles2d[i]);
            triangles.Add(topOffset + triangles2d[i + 2]);
            triangles.Add(topOffset + triangles2d[i + 1]);
        }

        for (var i = 0; i < basePositions.Count; i++)
        {
            var next = (i + 1) % basePositions.Count;
            var wallOffset = vertices.Count;
            vertices.Add(basePositions[i]);
            vertices.Add(basePositions[next]);
            vertices.Add(basePositions[next] + Vector3.up * height);
            vertices.Add(basePositions[i] + Vector3.up * height);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            triangles.Add(wallOffset);
            triangles.Add(wallOffset + 3);
            triangles.Add(wallOffset + 2);
            triangles.Add(wallOffset);
            triangles.Add(wallOffset + 2);
            triangles.Add(wallOffset + 1);
        }

        var mesh = new Mesh
        {
            name = "TerrainForger_Building"
        };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static List<GeoCoordinate> SanitizeRing(List<GeoCoordinate> footprint)
    {
        var result = new List<GeoCoordinate>();
        if (footprint == null)
        {
            return result;
        }

        for (var i = 0; i < footprint.Count; i++)
        {
            var current = footprint[i];
            if (result.Count > 0)
            {
                var previous = result[result.Count - 1];
                if (Math.Abs(previous.latitude - current.latitude) < 0.000000001d &&
                    Math.Abs(previous.longitude - current.longitude) < 0.000000001d)
                {
                    continue;
                }
            }

            result.Add(current);
        }

        if (result.Count > 1)
        {
            var first = result[0];
            var last = result[result.Count - 1];
            if (Math.Abs(first.latitude - last.latitude) < 0.000000001d &&
                Math.Abs(first.longitude - last.longitude) < 0.000000001d)
            {
                result.RemoveAt(result.Count - 1);
            }
        }

        return result;
    }

    private static Vector3 CalculateCentroid(List<Vector3> positions)
    {
        var result = Vector3.zero;
        for (var i = 0; i < positions.Count; i++)
        {
            result += positions[i];
        }

        return positions.Count == 0 ? result : result / positions.Count;
    }

    private static float SignedArea(List<Vector3> positions)
    {
        var area = 0f;
        for (var i = 0; i < positions.Count; i++)
        {
            var next = (i + 1) % positions.Count;
            area += (positions[i].x * positions[next].z) - (positions[next].x * positions[i].z);
        }

        return area * 0.5f;
    }

    private static List<int> Triangulate(List<Vector3> positions)
    {
        var indices = new List<int>();
        var remaining = new List<int>();
        for (var i = 0; i < positions.Count; i++)
        {
            remaining.Add(i);
        }

        var guard = 0;
        while (remaining.Count > 3 && guard < positions.Count * positions.Count)
        {
            guard++;
            var earFound = false;
            for (var i = 0; i < remaining.Count; i++)
            {
                var previousIndex = remaining[(i + remaining.Count - 1) % remaining.Count];
                var currentIndex = remaining[i];
                var nextIndex = remaining[(i + 1) % remaining.Count];

                if (!IsConvex(positions[previousIndex], positions[currentIndex], positions[nextIndex]))
                {
                    continue;
                }

                if (ContainsAnyPoint(positions, remaining, previousIndex, currentIndex, nextIndex))
                {
                    continue;
                }

                indices.Add(previousIndex);
                indices.Add(currentIndex);
                indices.Add(nextIndex);
                remaining.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
            {
                break;
            }
        }

        if (remaining.Count == 3)
        {
            indices.Add(remaining[0]);
            indices.Add(remaining[1]);
            indices.Add(remaining[2]);
        }

        return indices;
    }

    private static bool IsConvex(Vector3 a, Vector3 b, Vector3 c)
    {
        var cross = ((b.x - a.x) * (c.z - a.z)) - ((b.z - a.z) * (c.x - a.x));
        return cross > 0f;
    }

    private static bool ContainsAnyPoint(List<Vector3> positions, List<int> remaining, int a, int b, int c)
    {
        for (var i = 0; i < remaining.Count; i++)
        {
            var index = remaining[i];
            if (index == a || index == b || index == c)
            {
                continue;
            }

            if (PointInTriangle(positions[index], positions[a], positions[b], positions[c]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);
        var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNegative && hasPositive);
    }

    private static float Sign(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return ((p1.x - p3.x) * (p2.z - p3.z)) - ((p2.x - p3.x) * (p1.z - p3.z));
    }
}

public static class RoadGenerator
{
    public static Mesh BuildMesh(List<GeoCoordinate> centerline, float width, Terrain terrain, GeoToUnityTransform transform, float elevationOffset)
    {
        var points = SanitizeLine(centerline, terrain, transform);
        if (points.Count < 2)
        {
            return null;
        }

        var halfWidth = Mathf.Max(0.25f, width * 0.5f);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();

        for (var i = 0; i < points.Count; i++)
        {
            var direction = ResolveDirection(points, i);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return null;
            }

            var perpendicular = new Vector3(-direction.z, 0f, direction.x) * halfWidth;
            var left = points[i] + perpendicular;
            var right = points[i] - perpendicular;
            left.y = transform.SampleHeight(left, terrain) + elevationOffset;
            right.y = transform.SampleHeight(right, terrain) + elevationOffset;
            vertices.Add(left);
            vertices.Add(right);
            uvs.Add(new Vector2(0f, i));
            uvs.Add(new Vector2(1f, i));
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            var left0 = i * 2;
            var right0 = left0 + 1;
            var left1 = (i + 1) * 2;
            var right1 = left1 + 1;

            triangles.Add(left0);
            triangles.Add(left1);
            triangles.Add(right1);
            triangles.Add(left0);
            triangles.Add(right1);
            triangles.Add(right0);
        }

        var mesh = new Mesh
        {
            name = "TerrainForger_Road"
        };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static List<Vector3> SanitizeLine(List<GeoCoordinate> centerline, Terrain terrain, GeoToUnityTransform transform)
    {
        var result = new List<Vector3>();
        if (centerline == null)
        {
            return result;
        }

        for (var i = 0; i < centerline.Count; i++)
        {
            var position = transform.ToUnityPosition(centerline[i], terrain);
            if (result.Count > 0 && (result[result.Count - 1] - position).sqrMagnitude < 0.0001f)
            {
                continue;
            }

            result.Add(position);
        }

        return result;
    }

    private static Vector3 ResolveDirection(List<Vector3> points, int index)
    {
        Vector3 direction;
        if (index == 0)
        {
            direction = points[1] - points[0];
        }
        else if (index == points.Count - 1)
        {
            direction = points[index] - points[index - 1];
        }
        else
        {
            direction = points[index + 1] - points[index - 1];
        }

        direction.y = 0f;
        return direction.normalized;
    }
}

public static class TerrainForgerUrbanAssetWriter
{
    public static string CreateMeshAsset(Mesh mesh, string folder, string fileName)
    {
        TerrainForgerVectorExternalTools.EnsureAssetFolder(folder);
        var assetPath = folder.TrimEnd('/') + "/" + MakeSafeName(fileName) + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        AssetDatabase.CreateAsset(mesh, assetPath);
        return assetPath;
    }

    public static Material GetOrCreateMaterial(string assetPath, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material != null)
        {
            return material;
        }

        var shader = Shader.Find("Standard");
        material = new Material(shader);
        material.name = Path.GetFileNameWithoutExtension(assetPath);
        material.color = color;
        var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets";
        TerrainForgerVectorExternalTools.EnsureAssetFolder(folder);
        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }

    public static string MakeSafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Generated";
        }

        var result = Regex.Replace(value, @"[^A-Za-z0-9_\-]+", "_");
        result = result.Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "Generated" : result;
    }
}
