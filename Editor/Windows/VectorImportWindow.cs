using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class VectorImportWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private Terrain targetTerrain;
    private static readonly List<string> workflowLog = new List<string>();

    [MenuItem("TerrainForger/Import Vector Data")]
    public static void Open()
    {
        var window = GetWindow<VectorImportWindow>("Import Vector Data");
        window.minSize = new Vector2(780f, 640f);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        var settings = TerrainForgerVectorImportSettings.instance;

        TerrainForgeWindowUtility.DrawSettingsHeader(
            TerrainForgeWorkflowSettings.instance,
            "TerrainForger: Import Vector Data",
            "Import OpenStreetMap or local vector data, normalize it to TerrainForger vector layers, and generate urban meshes aligned to an existing Terrain.");

        EditorGUI.BeginChangeCheck();
        DrawSourceSection(settings);
        DrawBoundsSection(settings);
        DrawLayersSection(settings);
        DrawAttributeMappingSection(settings);
        DrawOutputSection(settings);

        if (EditorGUI.EndChangeCheck())
        {
            settings.SaveSettings();
        }

        DrawDownloadAction(settings);

        EditorGUI.BeginChangeCheck();
        DrawGenerationSection(settings);
        if (EditorGUI.EndChangeCheck())
        {
            settings.SaveSettings();
        }

        DrawWorkflowLog();
        DrawVectorSettingsFooter(settings);
        EditorGUILayout.EndScrollView();
    }

    private void DrawSourceSection(TerrainForgerVectorImportSettings settings)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("1. Source", EditorStyles.boldLabel);
            settings.sourceType = (TerrainForgerVectorSourceType)EditorGUILayout.EnumPopup(
                new GUIContent("Source Type", "Choose OpenStreetMap Overpass or a local vector file converted through GDAL/OGR."),
                settings.sourceType);

            if (settings.sourceType == TerrainForgerVectorSourceType.OpenStreetMapOverpass)
            {
                settings.overpassEndpoint = EditorGUILayout.TextField(
                    new GUIContent("Overpass Endpoint", "Overpass API endpoint used for OpenStreetMap building and road queries."),
                    settings.overpassEndpoint);
            }
            else
            {
                settings.localVectorFilePath = EditorGUILayout.TextField(
                    new GUIContent("Vector File", "Local .shp, .geojson, .gpkg or .kml file to import through GDAL/OGR."),
                    settings.localVectorFilePath);
                if (GUILayout.Button(new GUIContent("Browse Vector File", "Choose a local vector dataset.")))
                {
                    BrowseVectorFile(settings);
                }

                settings.localLayerName = EditorGUILayout.TextField(
                    new GUIContent("Layer Name", "Optional layer name for GeoPackage or multi-layer vector sources."),
                    settings.localLayerName);
                settings.localLayerMode = (TerrainForgerVectorLayerSelectionMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Layer Interpretation", "Auto infers buildings/roads from attributes. Buildings or Roads forces all local features into that layer."),
                    settings.localLayerMode);
            }
        }
    }

    private void DrawBoundsSection(TerrainForgerVectorImportSettings settings)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("2. Bounds", EditorStyles.boldLabel);
            settings.useTerrainForgerBounds = EditorGUILayout.Toggle(
                new GUIContent("Use Current TerrainForger Bounds", "Use the bounds from UserSettings/TerrainForgeWorkflowSettings.asset."),
                settings.useTerrainForgerBounds);

            if (settings.useTerrainForgerBounds)
            {
                var bounds = GeoBounds.FromWorkflowSettings(TerrainForgeWorkflowSettings.instance);
                EditorGUILayout.LabelField("North", bounds.north.ToString("0.########"));
                EditorGUILayout.LabelField("South", bounds.south.ToString("0.########"));
                EditorGUILayout.LabelField("West", bounds.west.ToString("0.########"));
                EditorGUILayout.LabelField("East", bounds.east.ToString("0.########"));
            }
            else
            {
                settings.manualNorthBound = TerrainForgeWindowUtility.DrawLatitudeDdmField("North Bound", settings.manualNorthBound);
                settings.manualSouthBound = TerrainForgeWindowUtility.DrawLatitudeDdmField("South Bound", settings.manualSouthBound);
                settings.manualWestBound = TerrainForgeWindowUtility.DrawLongitudeDdmField("West Bound", settings.manualWestBound);
                settings.manualEastBound = TerrainForgeWindowUtility.DrawLongitudeDdmField("East Bound", settings.manualEastBound);
            }
        }
    }

    private void DrawLayersSection(TerrainForgerVectorImportSettings settings)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("3. Layers", EditorStyles.boldLabel);
            settings.importBuildings = EditorGUILayout.Toggle(
                new GUIContent("Buildings", "Import and generate building footprints as extruded meshes."),
                settings.importBuildings);
            settings.importRoads = EditorGUILayout.Toggle(
                new GUIContent("Roads", "Import and generate roads as terrain-conformed mesh strips."),
                settings.importRoads);
        }
    }

    private void DrawAttributeMappingSection(TerrainForgerVectorImportSettings settings)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("4. Attribute Mapping", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Leave fields empty to use automatic OSM/common-name detection.", MessageType.None);
            settings.heightField = EditorGUILayout.TextField(new GUIContent("Height Field", "Building height field. Auto checks height and render_height."), settings.heightField);
            settings.levelsField = EditorGUILayout.TextField(new GUIContent("Levels Field", "Building floor count field. Auto checks building:levels and levels."), settings.levelsField);
            settings.buildingTypeField = EditorGUILayout.TextField(new GUIContent("Building Type Field", "Optional building type field used by fallback height rules."), settings.buildingTypeField);
            settings.roadClassField = EditorGUILayout.TextField(new GUIContent("Road Class Field", "Road class field. Auto checks highway, class and road_class."), settings.roadClassField);
            settings.widthField = EditorGUILayout.TextField(new GUIContent("Width Field", "Road width field. Auto checks width."), settings.widthField);
        }
    }

    private void DrawGenerationSection(TerrainForgerVectorImportSettings settings)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("6. Placement", EditorStyles.boldLabel);
            targetTerrain = (Terrain)EditorGUILayout.ObjectField(
                new GUIContent("Target Terrain", "Terrain used for height sampling and vector alignment. If empty, generated meshes use a flat Y=0 base."),
                targetTerrain,
                typeof(Terrain),
                true);

            EditorGUILayout.HelpBox("Download vector data first. Placement reads the normalized GeoJSON files from the output folder.", MessageType.Info);

            EditorGUILayout.LabelField("Building Settings", EditorStyles.boldLabel);
            settings.defaultBuildingHeight = EditorGUILayout.FloatField(new GUIContent("Default Height", "Fallback building height in meters."), settings.defaultBuildingHeight);
            settings.floorHeight = EditorGUILayout.FloatField(new GUIContent("Floor Height", "Meters per building level when only level count exists."), settings.floorHeight);
            settings.minimumBuildingHeight = EditorGUILayout.FloatField(new GUIContent("Minimum Height", "Minimum generated building height in meters."), settings.minimumBuildingHeight);
            settings.maximumBuildingHeight = EditorGUILayout.FloatField(new GUIContent("Maximum Height", "Maximum generated building height clamp in meters."), settings.maximumBuildingHeight);
            settings.generateBuildingColliders = EditorGUILayout.Toggle(
                new GUIContent("Generate Building Colliders", "Add MeshCollider components to generated building objects. Disabled by default for import performance."),
                settings.generateBuildingColliders);

            EditorGUILayout.LabelField("Road Settings", EditorStyles.boldLabel);
            settings.generateRoadMeshes = EditorGUILayout.Toggle(new GUIContent("Generate Road Meshes", "Generate roads as mesh strips from centerlines."), settings.generateRoadMeshes);
            settings.defaultRoadWidth = EditorGUILayout.FloatField(new GUIContent("Default Road Width", "Fallback road width in meters."), settings.defaultRoadWidth);
            settings.laneWidth = EditorGUILayout.FloatField(new GUIContent("Lane Width", "Width per lane when the lanes attribute exists."), settings.laneWidth);
            settings.roadElevationOffset = EditorGUILayout.FloatField(new GUIContent("Road Elevation Offset", "Vertical road offset above terrain in meters to reduce z-fighting."), settings.roadElevationOffset);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Place Buildings", "Create or replace generated building meshes from normalized_buildings.geojson."), GUILayout.Height(30f)))
                {
                    PlaceVectorLayer(settings, VectorLayerKind.Building);
                }

                if (GUILayout.Button(new GUIContent("Place Roads", "Create or replace generated road meshes from normalized_roads.geojson."), GUILayout.Height(30f)))
                {
                    PlaceVectorLayer(settings, VectorLayerKind.Road);
                }
            }
        }
    }

    private void DrawOutputSection(TerrainForgerVectorImportSettings settings)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("5. Output", EditorStyles.boldLabel);
            settings.outputFolder = EditorGUILayout.TextField(
                new GUIContent("Output Folder", "Generated vector assets folder. Must stay inside Assets."),
                settings.outputFolder);
            settings.saveNormalizedGeoJson = true;
            EditorGUILayout.LabelField(
                new GUIContent("Normalized GeoJSON", "Placement requires normalized_buildings.geojson and normalized_roads.geojson."),
                new GUIContent("Always saved"));

            if (GUILayout.Button(new GUIContent("Reveal Output Folder", "Open the configured output folder.")))
            {
                TerrainForgeWindowUtility.RevealFolder(settings.outputFolder, "Vector Output Folder Missing");
            }
        }
    }

    private void DrawDownloadAction(TerrainForgerVectorImportSettings settings)
    {
        EditorGUILayout.Space();
        if (GUILayout.Button(new GUIContent("Download Vector Data", "Download or convert selected vector data and write normalized GeoJSON/report files for later placement."), GUILayout.Height(34f)))
        {
            DownloadVectorData(settings);
        }
    }

    private void DownloadVectorData(TerrainForgerVectorImportSettings settings)
    {
        try
        {
            ValidateSettings(settings);
            settings.saveNormalizedGeoJson = true;
            settings.SaveSettings();
            var bounds = settings.ResolveBounds();
            var context = new TerrainForgerVectorImportContext
            {
                Bounds = bounds,
                Log = AddLog
            };

            var provider = CreateProvider(settings.sourceType);
            AddLog("Provider: " + provider.DisplayName);
            EditorUtility.DisplayProgressBar("TerrainForger Vector Data", "Downloading and normalizing vector data.", 0.25f);
            var dataset = provider.Import(settings, context);
            dataset.Bounds = bounds;
            dataset = FilterDataset(dataset, settings);

            SaveNormalizedGeoJson(dataset, settings);
            var report = BuildImportOnlyReport(dataset, settings, bounds);
            SaveImportReport(report, settings);
            AddLog("Vector data download complete.");
            EditorUtility.DisplayDialog("Vector Data Ready", "Vector data was downloaded and normalized. Use Place Buildings or Place Roads to generate Unity objects.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Vector Data Download Failed", ex.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void PlaceVectorLayer(TerrainForgerVectorImportSettings settings, VectorLayerKind kind)
    {
        try
        {
            ValidatePlacementSettings(settings, kind);
            settings.SaveSettings();
            var dataset = LoadNormalizedDataset(settings, kind);
            var placeBuildings = kind == VectorLayerKind.Building;
            var placeRoads = kind == VectorLayerKind.Road;
            var generationResult = TerrainForgerUrbanGenerator.Generate(
                dataset,
                settings,
                targetTerrain,
                AddLog,
                placeBuildings,
                placeRoads);

            if (generationResult.RootObject != null)
            {
                Selection.activeGameObject = generationResult.RootObject;
            }

            SavePlacementReport(generationResult.Report, settings, kind);
            AddLog(GetLayerDisplayName(kind) + " placement complete.");
            EditorUtility.DisplayDialog(GetLayerDisplayName(kind) + " Placement Complete", GetLayerDisplayName(kind) + " placement completed successfully.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog(GetLayerDisplayName(kind) + " Placement Failed", ex.Message, "OK");
        }
    }

    private static void ValidateSettings(TerrainForgerVectorImportSettings settings)
    {
        var outputFolder = settings.outputFolder.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrWhiteSpace(outputFolder) || !outputFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Output folder must be inside Assets, for example Assets/Generated/TerrainForger.");
        }

        if (!settings.importBuildings && !settings.importRoads)
        {
            throw new InvalidOperationException("Enable at least one layer before importing vector data.");
        }

        var bounds = settings.ResolveBounds();
        if (!bounds.IsValid)
        {
            throw new InvalidOperationException("Vector import bounds are invalid.");
        }
    }

    private static void ValidatePlacementSettings(TerrainForgerVectorImportSettings settings, VectorLayerKind kind)
    {
        ValidateOutputFolder(settings);

        if (kind == VectorLayerKind.Road && !settings.generateRoadMeshes)
        {
            throw new InvalidOperationException("Enable Generate Road Meshes before placing roads.");
        }

        var bounds = ResolveSavedVectorBounds(settings);
        if (!bounds.IsValid)
        {
            throw new InvalidOperationException("Vector placement bounds are invalid. Download vector data again before placing objects.");
        }

        var geoJsonPath = GetNormalizedGeoJsonFullPath(settings, kind);
        if (!File.Exists(geoJsonPath))
        {
            throw new FileNotFoundException("Normalized " + GetLayerDisplayName(kind).ToLowerInvariant() + " GeoJSON was not found. Download vector data before placing this layer.", geoJsonPath);
        }
    }

    private static void ValidateOutputFolder(TerrainForgerVectorImportSettings settings)
    {
        var outputFolder = settings.outputFolder.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrWhiteSpace(outputFolder) || !outputFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Output folder must be inside Assets, for example Assets/Generated/TerrainForger.");
        }
    }

    private static IVectorDataProvider CreateProvider(TerrainForgerVectorSourceType sourceType)
    {
        switch (sourceType)
        {
            case TerrainForgerVectorSourceType.LocalVectorFile:
                return new LocalVectorFileProvider();
            default:
                return new OverpassVectorProvider();
        }
    }

    private static VectorDataset FilterDataset(VectorDataset dataset, TerrainForgerVectorImportSettings settings)
    {
        var filtered = new VectorDataset
        {
            Bounds = dataset.Bounds,
            SourceName = dataset.SourceName
        };

        for (var i = 0; i < dataset.Layers.Count; i++)
        {
            var layer = dataset.Layers[i];
            if (layer.Kind == VectorLayerKind.Building && !settings.importBuildings)
            {
                continue;
            }

            if (layer.Kind == VectorLayerKind.Road && !settings.importRoads)
            {
                continue;
            }

            filtered.Layers.Add(layer);
        }

        return filtered;
    }

    private static void SaveNormalizedGeoJson(VectorDataset dataset, TerrainForgerVectorImportSettings settings)
    {
        var vectorFolder = settings.outputFolder.Replace('\\', '/').TrimEnd('/') + "/Vector";
        TerrainForgerVectorExternalTools.EnsureAssetFolder(vectorFolder);

        var vectorFullPath = TerrainForgerVectorExternalTools.ResolvePath(vectorFolder);
        WriteOrDeleteNormalizedGeoJson(dataset, VectorLayerKind.Building, Path.Combine(vectorFullPath, "normalized_buildings.geojson"));
        WriteOrDeleteNormalizedGeoJson(dataset, VectorLayerKind.Road, Path.Combine(vectorFullPath, "normalized_roads.geojson"));

        AssetDatabase.Refresh();
    }

    private static void WriteOrDeleteNormalizedGeoJson(VectorDataset dataset, VectorLayerKind kind, string path)
    {
        if (TerrainForgerGeoJsonUtility.CountFeatures(dataset, kind) > 0)
        {
            File.WriteAllText(path, TerrainForgerGeoJsonUtility.WriteFeatureCollection(dataset, kind));
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static VectorDataset LoadNormalizedDataset(TerrainForgerVectorImportSettings settings, VectorLayerKind kind)
    {
        var path = GetNormalizedGeoJsonFullPath(settings, kind);
        var geoJson = File.ReadAllText(path);
        var layerMode = kind == VectorLayerKind.Building
            ? TerrainForgerVectorLayerSelectionMode.Buildings
            : TerrainForgerVectorLayerSelectionMode.Roads;
        var dataset = TerrainForgerGeoJsonUtility.ReadDataset(
            geoJson,
            Path.GetFileName(path),
            layerMode);
        dataset.Bounds = ResolveSavedVectorBounds(settings);
        return dataset;
    }

    private static TerrainForgerVectorImportReport BuildImportOnlyReport(
        VectorDataset dataset,
        TerrainForgerVectorImportSettings settings,
        GeoBounds bounds)
    {
        return new TerrainForgerVectorImportReport
        {
            source = dataset.SourceName,
            bounds = bounds,
            buildingCount = TerrainForgerGeoJsonUtility.CountFeatures(dataset, VectorLayerKind.Building),
            roadCount = TerrainForgerGeoJsonUtility.CountFeatures(dataset, VectorLayerKind.Road),
            generationDateUtc = DateTime.UtcNow.ToString("o")
        };
    }

    private static void SaveImportReport(TerrainForgerVectorImportReport report, TerrainForgerVectorImportSettings settings)
    {
        var vectorFolder = settings.outputFolder.Replace('\\', '/').TrimEnd('/') + "/Vector";
        TerrainForgerVectorExternalTools.EnsureAssetFolder(vectorFolder);
        var vectorFullPath = TerrainForgerVectorExternalTools.ResolvePath(vectorFolder);
        File.WriteAllText(Path.Combine(vectorFullPath, "import_report.json"), JsonUtility.ToJson(report, true));
        AssetDatabase.Refresh();
    }

    private static void SavePlacementReport(TerrainForgerVectorImportReport report, TerrainForgerVectorImportSettings settings, VectorLayerKind kind)
    {
        var vectorFolder = settings.outputFolder.Replace('\\', '/').TrimEnd('/') + "/Vector";
        TerrainForgerVectorExternalTools.EnsureAssetFolder(vectorFolder);
        var vectorFullPath = TerrainForgerVectorExternalTools.ResolvePath(vectorFolder);
        var fileName = kind == VectorLayerKind.Building
            ? "placement_buildings_report.json"
            : "placement_roads_report.json";
        File.WriteAllText(Path.Combine(vectorFullPath, fileName), JsonUtility.ToJson(report, true));
        AssetDatabase.Refresh();
    }

    private static GeoBounds ResolveSavedVectorBounds(TerrainForgerVectorImportSettings settings)
    {
        var reportPath = GetImportReportFullPath(settings);
        if (File.Exists(reportPath))
        {
            var report = JsonUtility.FromJson<TerrainForgerVectorImportReport>(File.ReadAllText(reportPath));
            if (report != null && report.bounds.IsValid)
            {
                return report.bounds;
            }
        }

        return settings.ResolveBounds();
    }

    private static string GetImportReportFullPath(TerrainForgerVectorImportSettings settings)
    {
        var vectorFolder = settings.outputFolder.Replace('\\', '/').TrimEnd('/') + "/Vector";
        return Path.Combine(TerrainForgerVectorExternalTools.ResolvePath(vectorFolder), "import_report.json");
    }

    private static string GetNormalizedGeoJsonFullPath(TerrainForgerVectorImportSettings settings, VectorLayerKind kind)
    {
        var vectorFolder = settings.outputFolder.Replace('\\', '/').TrimEnd('/') + "/Vector";
        var fileName = kind == VectorLayerKind.Building
            ? "normalized_buildings.geojson"
            : "normalized_roads.geojson";
        return Path.Combine(TerrainForgerVectorExternalTools.ResolvePath(vectorFolder), fileName);
    }

    private static string GetLayerDisplayName(VectorLayerKind kind)
    {
        return kind == VectorLayerKind.Building ? "Buildings" : "Roads";
    }

    private static void BrowseVectorFile(TerrainForgerVectorImportSettings settings)
    {
        var startFolder = string.IsNullOrWhiteSpace(settings.localVectorFilePath)
            ? TerrainForgeWindowUtility.ResolveFolderPath("Assets")
            : Path.GetDirectoryName(TerrainForgeWindowUtility.ResolveFolderPath(settings.localVectorFilePath));

        var selected = EditorUtility.OpenFilePanel("Select Vector File", startFolder, "shp,geojson,json,gpkg,kml");
        if (string.IsNullOrEmpty(selected))
        {
            return;
        }

        settings.localVectorFilePath = selected;
        settings.SaveSettings();
        GUIUtility.ExitGUI();
    }

    private static void AddLog(string message)
    {
        workflowLog.Add(string.Format("{0:HH:mm:ss} - {1}", DateTime.Now, message));
        while (workflowLog.Count > 16)
        {
            workflowLog.RemoveAt(0);
        }
    }

    private static void DrawWorkflowLog()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("7. Log", EditorStyles.boldLabel);
            if (workflowLog.Count == 0)
            {
                EditorGUILayout.HelpBox("No vector import steps have run yet.", MessageType.Info);
                return;
            }

            for (var i = 0; i < workflowLog.Count; i++)
            {
                EditorGUILayout.LabelField(workflowLog[i]);
            }
        }
    }

    private static void DrawVectorSettingsFooter(TerrainForgerVectorImportSettings settings)
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Save Vector Settings", "Persist the current vector import settings into UserSettings/TerrainForgerVectorImportSettings.asset."), GUILayout.Width(170f)))
            {
                settings.SaveSettings();
            }

            if (GUILayout.Button(new GUIContent("Reset Vector Settings", "Reset only this vector import module settings."), GUILayout.Width(170f)))
            {
                ResetVectorSettings(settings);
                GUIUtility.ExitGUI();
            }
        }
    }

    private static void ResetVectorSettings(TerrainForgerVectorImportSettings settings)
    {
        var defaults = ScriptableObject.CreateInstance<TerrainForgerVectorImportSettings>();
        try
        {
            EditorUtility.CopySerialized(defaults, settings);
            settings.SaveSettings();
        }
        finally
        {
            DestroyImmediate(defaults);
        }
    }
}
