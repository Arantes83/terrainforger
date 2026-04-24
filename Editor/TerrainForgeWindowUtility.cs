using System.IO;
using UnityEditor;
using UnityEngine;

public static class TerrainForgeWindowUtility
{
    public static void DrawSettingsHeader(TerrainForgeWorkflowSettings settings, string title, string description)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(description, MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Service Settings", GUILayout.Width(120f)))
            {
                TerrainDataServiceSettingsProvider.OpenSettings();
            }

            if (GUILayout.Button("Save Tool Settings", GUILayout.Width(140f)))
            {
                settings.SaveSettings();
            }

            if (GUILayout.Button("Reset Tool Settings", GUILayout.Width(140f)))
            {
                ResetSettings(settings);
                GUIUtility.ExitGUI();
            }
        }
    }

    public static void DrawCurrentSummary(TerrainForgeWorkflowSettings settings)
    {
        var qgisInstallFolder = TerrainDataServiceSettings.instance.QgisInstallFolder;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Current Settings Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("GeoTIFF", string.IsNullOrWhiteSpace(settings.geoTiffPath) ? "(not set)" : settings.geoTiffPath);
            EditorGUILayout.LabelField("QGIS Install Folder", string.IsNullOrWhiteSpace(qgisInstallFolder) ? "(not set)" : qgisInstallFolder);
            EditorGUILayout.LabelField("RAW Folder", settings.inputFolder);
            EditorGUILayout.LabelField("Terrain Asset Folder", settings.outputFolder);
            EditorGUILayout.LabelField("Grid", $"{settings.cols} cols x {settings.rows} rows");
            EditorGUILayout.LabelField("Resolution", settings.heightmapResolution.ToString());
            EditorGUILayout.LabelField("Pattern", settings.filePattern);
        }
    }

    public static void DrawImportSummary(TerrainForgeWorkflowSettings settings)
    {
        var inferredLayout = TerrainTileImporter.InspectInputFolder(ResolveFolderPath(settings.inputFolder));
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Current Import Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("RAW Folder", string.IsNullOrWhiteSpace(settings.inputFolder) ? "(not set)" : settings.inputFolder);
            EditorGUILayout.LabelField("PNG Folder", string.IsNullOrWhiteSpace(settings.satelliteOutputFolder) ? "(not set)" : settings.satelliteOutputFolder);
            EditorGUILayout.LabelField("Terrain Asset Folder", string.IsNullOrWhiteSpace(settings.outputFolder) ? "(not set)" : settings.outputFolder);
            EditorGUILayout.LabelField(
                "Grid",
                inferredLayout.isValid ? $"{inferredLayout.cols} cols x {inferredLayout.rows} rows" : "(could not infer)");
            EditorGUILayout.LabelField(
                "Heightmap Resolution",
                inferredLayout.isValid ? inferredLayout.heightmapResolution.ToString() : "(could not infer)");
            EditorGUILayout.LabelField(
                "Tile Count",
                inferredLayout.isValid ? inferredLayout.tileCount.ToString() : "(could not infer)");
            EditorGUILayout.LabelField("Detection", inferredLayout.isValid ? inferredLayout.message : inferredLayout.message);
            EditorGUILayout.LabelField("Root Object", string.IsNullOrWhiteSpace(settings.rootObjectName) ? "(not set)" : settings.rootObjectName);
        }
    }

    public static void DrawSharedTileProperties(SerializedObject serializedObject)
    {
        DrawProperty(serializedObject, "rows", "Rows");
        DrawProperty(serializedObject, "cols", "Columns");
        DrawProperty(serializedObject, "heightmapResolution", "Heightmap Resolution");
        DrawProperty(serializedObject, "filePattern", "File Pattern");
    }

    public static void DrawProperty(SerializedObject serializedObject, string propertyName, string label)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren: true);
        }
    }

    public static int DrawIntField(string label, int value, int minValue = int.MinValue, int maxValue = int.MaxValue)
    {
        EditorGUI.BeginChangeCheck();
        var newValue = EditorGUILayout.IntField(label, value);
        if (EditorGUI.EndChangeCheck())
        {
            return Mathf.Clamp(newValue, minValue, maxValue);
        }

        return value;
    }

    public static LatitudeDdm DrawLatitudeDdmField(string label, LatitudeDdm value)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            value.hemisphere = (LatitudeHemisphere)EditorGUILayout.EnumPopup("Hemisphere", value.hemisphere);
            value.degrees = DrawIntField("Degrees", value.degrees, 0, 90);
            value.minutes = DrawIntField("Minutes", value.minutes, 0, 59);
            value.tenthsOfMinutes = DrawIntField("Tenths Of Minutes", value.tenthsOfMinutes, 0, 9);
        }

        return value;
    }

    public static LongitudeDdm DrawLongitudeDdmField(string label, LongitudeDdm value)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            value.hemisphere = (LongitudeHemisphere)EditorGUILayout.EnumPopup("Hemisphere", value.hemisphere);
            value.degrees = DrawIntField("Degrees", value.degrees, 0, 180);
            value.minutes = DrawIntField("Minutes", value.minutes, 0, 59);
            value.tenthsOfMinutes = DrawIntField("Tenths Of Minutes", value.tenthsOfMinutes, 0, 9);
        }

        return value;
    }

    public static void DrawPathButtons(TerrainForgeWorkflowSettings settings, bool includeGeoTiffButton)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (includeGeoTiffButton && GUILayout.Button("Browse GeoTIFF"))
            {
                BrowseGeoTiff(settings);
            }
        }
    }

    public static int DrawConfiguredProviderPopup(
        string label,
        TerrainBuiltInProviderInfo[] providers,
        string currentProviderId)
    {
        var displayOptions = new string[providers.Length];
        var currentIndex = 0;

        for (var i = 0; i < providers.Length; i++)
        {
            displayOptions[i] = providers[i].displayName;
            if (providers[i].providerId == currentProviderId)
            {
                currentIndex = i;
            }
        }

        return EditorGUILayout.Popup(label, currentIndex, displayOptions);
    }

    public static TerrainBuiltInProviderInfo[] GetConfiguredProviders(
        bool supportsElevation,
        bool supportsImagery,
        params string[] supportedProviderIds)
    {
        var settings = TerrainDataServiceSettings.instance;
        var providers = TerrainDataServiceSettings.GetBuiltInProviders();
        var filtered = new System.Collections.Generic.List<TerrainBuiltInProviderInfo>();
        var enforceSupportedProviderIds = supportedProviderIds != null && supportedProviderIds.Length > 0;

        for (var i = 0; i < providers.Count; i++)
        {
            var provider = providers[i];
            if (supportsElevation && !provider.supportsElevation)
            {
                continue;
            }

            if (supportsImagery && !provider.supportsImagery)
            {
                continue;
            }

            if (enforceSupportedProviderIds && System.Array.IndexOf(supportedProviderIds, provider.providerId) < 0)
            {
                continue;
            }

            if (!provider.IsConfigured(settings))
            {
                continue;
            }

            filtered.Add(provider);
        }

        return filtered.ToArray();
    }

    public static void BrowseGeoTiff(TerrainForgeWorkflowSettings settings)
    {
        var startFolder = string.IsNullOrWhiteSpace(settings.geoTiffPath)
            ? ResolveFolderPath("Assets")
            : Path.GetDirectoryName(ResolveFolderPath(settings.geoTiffPath));

        var selected = EditorUtility.OpenFilePanel("Select GeoTIFF", startFolder, string.Empty);
        if (!string.IsNullOrEmpty(selected))
        {
            settings.geoTiffPath = selected;
            settings.SaveSettings();
        }
    }

    public static void RevealFolder(string folderPath, string title)
    {
        var resolvedPath = ResolveFolderPath(folderPath);
        if (Directory.Exists(resolvedPath))
        {
            EditorUtility.RevealInFinder(resolvedPath);
        }
        else
        {
            EditorUtility.DisplayDialog(title, $"Folder not found:\n{resolvedPath}", "OK");
        }
    }

    public static string ResolveFolderPath(string folderPath)
    {
        if (Path.IsPathRooted(folderPath))
        {
            return folderPath;
        }

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.GetFullPath(Path.Combine(projectRoot, folderPath));
    }

    public static void ExecuteWithRuntimeConfig(
        TerrainForgeWorkflowSettings settings,
        System.Action<TerrainTileImportConfig> action)
    {
        var runtimeConfig = settings.CreateRuntimeConfig();
        try
        {
            action(runtimeConfig);
            settings.CopyFromRuntimeConfig(runtimeConfig);
            settings.SaveSettings();
        }
        finally
        {
            Object.DestroyImmediate(runtimeConfig);
        }
    }

    private static void ResetSettings(TerrainForgeWorkflowSettings settings)
    {
        var defaults = ScriptableObject.CreateInstance<TerrainForgeWorkflowSettings>();
        try
        {
            EditorUtility.CopySerialized(defaults, settings);
            settings.SaveSettings();
        }
        finally
        {
            Object.DestroyImmediate(defaults);
        }
    }
}
