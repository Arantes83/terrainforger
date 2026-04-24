using System.IO;
using UnityEditor;
using UnityEngine;

public class TerrainForgeDownloadGeoDataWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private Texture2D sourcePreviewTexture;
    private string sourcePreviewSourcePath = string.Empty;
    private string sourcePreviewStatus = "No source preview loaded.";
    private Texture2D demPreviewTexture;
    private string demPreviewSourcePath = string.Empty;
    private string demPreviewStatus = "No DEM preview loaded.";
    private Texture2D satellitePreviewTexture;
    private string satellitePreviewSourcePath = string.Empty;
    private string satellitePreviewStatus = "No satellite preview loaded.";
    private bool showBounds = true;

    [MenuItem("Tools/TerrainForger/Get GIS Data")]
    public static void Open()
    {
        var window = GetWindow<TerrainForgeDownloadGeoDataWindow>("Get GIS Data");
        window.minSize = new Vector2(620f, 520f);
        window.Show();
        window.Focus();
    }

    private void OnDisable()
    {
        ReleaseSourcePreviewTexture();
        ReleasePreviewTexture();
        ReleaseSatellitePreviewTexture();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        var settings = TerrainForgeWorkflowSettings.instance;

        TerrainForgeWindowUtility.DrawSettingsHeader(
            settings,
            "TerrainForger: Get GIS Data",
            "Load a local GeoTIFF or KAP chart, auto-fill bounds, choose DEM and imagery providers, and save downloaded data inside Assets/Terrain.");
        TerrainForgeWindowUtility.DrawPathButtons(settings, includeGeoTiffButton: false);

        var serializedObject = new SerializedObject(settings);
        serializedObject.Update();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Local Source", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Choose a local source file here. Use 'Store Source' to copy it into Assets/Terrain, and 'Refill Bounds From Source' only when you want to sync the bounds back from the selected file.",
                MessageType.None);
            TerrainForgeWindowUtility.DrawProperty(serializedObject, "localSourceType", "Source Type");
            TerrainForgeWindowUtility.DrawProperty(serializedObject, "localSourcePath", "Source File");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Browse Source File"))
                {
                    BrowseLocalSource(settings);
                }

                if (GUILayout.Button("Store Source"))
                {
                    RunStoreLocalSource(settings);
                }

                if (GUILayout.Button("Refill Bounds From Source"))
                {
                    RunRefillBoundsFromSource(settings);
                }
            }
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Providers", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Only providers that are already configured and currently supported by this tool appear in the dropdowns below.",
                MessageType.None);

            var demProviders = TerrainForgeWindowUtility.GetConfiguredProviders(
                supportsElevation: true,
                supportsImagery: false,
                TerrainDataProviderIds.OpenTopography);
            if (demProviders.Length == 0)
            {
                EditorGUILayout.HelpBox("No DEM providers are configured. Add credentials in Service Settings.", MessageType.Warning);
                settings.demProviderId = string.Empty;
            }
            else
            {
                var selectedDemIndex = TerrainForgeWindowUtility.DrawConfiguredProviderPopup("DEM Provider", demProviders, settings.demProviderId);
                settings.demProviderId = demProviders[selectedDemIndex].providerId;
            }

            var imageryProviders = TerrainForgeWindowUtility.GetConfiguredProviders(
                supportsElevation: false,
                supportsImagery: true,
                TerrainDataProviderIds.Mapbox);
            if (imageryProviders.Length == 0)
            {
                EditorGUILayout.HelpBox("No imagery providers are configured. Add credentials in Service Settings.", MessageType.Warning);
                settings.imageryProviderId = string.Empty;
            }
            else
            {
                var selectedImageryIndex = TerrainForgeWindowUtility.DrawConfiguredProviderPopup("Satellite Provider", imageryProviders, settings.imageryProviderId);
                settings.imageryProviderId = imageryProviders[selectedImageryIndex].providerId;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Satellite Resolution", EditorStyles.boldLabel);
            settings.satelliteResolution = EditorGUILayout.FloatField("Resolution", settings.satelliteResolution);
            settings.satelliteResolution = Mathf.Max(0.0001f, settings.satelliteResolution);
            settings.satelliteResolutionUnit = (TerrainForgerSatelliteResolutionUnit)EditorGUILayout.EnumPopup("Unit", settings.satelliteResolutionUnit);

            var plan = TerrainForgerGisDataUtility.BuildSatelliteDownloadPlan(settings);
            if (plan.isValid)
            {
                EditorGUILayout.LabelField("Map Size", $"{plan.widthMeters:0.##} m x {plan.heightMeters:0.##} m");
                EditorGUILayout.LabelField("Computed Resolution", $"{plan.pixelsPerMeter:0.####} px/m ({plan.metersPerPixel:0.####} m/px)");
                EditorGUILayout.LabelField("Output Raster", $"{plan.totalWidthPixels} x {plan.totalHeightPixels} px");
                EditorGUILayout.LabelField("Provider Limit", $"{plan.maxTileSize} px per tile");
                EditorGUILayout.LabelField("Tile Plan", $"{plan.tilesX} x {plan.tilesY} ({plan.totalTiles} tiles, max {plan.maxTileWidthPixels} x {plan.maxTileHeightPixels} px each)");

                if (!string.IsNullOrWhiteSpace(plan.warningMessage))
                {
                    EditorGUILayout.HelpBox(plan.warningMessage, MessageType.Warning);
                }
            }
            else if (!string.IsNullOrWhiteSpace(plan.warningMessage))
            {
                EditorGUILayout.HelpBox(plan.warningMessage, MessageType.Warning);
            }
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            showBounds = EditorGUILayout.Foldout(showBounds, "Map Bounds", true);
            if (showBounds)
            {
                settings.northBound = TerrainForgeWindowUtility.DrawLatitudeDdmField("North Bound", settings.northBound);
                settings.southBound = TerrainForgeWindowUtility.DrawLatitudeDdmField("South Bound", settings.southBound);
                settings.westBound = TerrainForgeWindowUtility.DrawLongitudeDdmField("West Bound", settings.westBound);
                settings.eastBound = TerrainForgeWindowUtility.DrawLongitudeDdmField("East Bound", settings.eastBound);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(260f), GUILayout.MaxWidth(360f)))
            {
                DrawSourcePreviewSection(settings);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(260f), GUILayout.MaxWidth(360f)))
            {
                DrawDemPreviewSection(settings);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(260f), GUILayout.MaxWidth(360f)))
            {
                DrawSatellitePreviewSection(settings);
            }
        }

        serializedObject.ApplyModifiedProperties();
        settings.SaveSettings();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Storage", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("DEM Folder", "Assets/Terrain/GeoTIFF");
            EditorGUILayout.LabelField("Satellite Folder", "Assets/Terrain/SAT");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reveal DEM Folder"))
                {
                    TerrainForgeWindowUtility.RevealFolder("Assets/Terrain/GeoTIFF", "DEM Folder Missing");
                }

                if (GUILayout.Button("Reveal SAT Folder"))
                {
                    TerrainForgeWindowUtility.RevealFolder("Assets/Terrain/SAT", "SAT Folder Missing");
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Download DEM"))
            {
                RunDownloadDem(settings);
            }

            if (GUILayout.Button("Download Satellite"))
            {
                RunDownloadSatellite(settings);
            }
        }

        if (GUILayout.Button("Get Selected GIS Data", GUILayout.Height(32f)))
        {
            RunDownloadAll(settings);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawDemPreviewSection(TerrainForgeWorkflowSettings settings)
    {
        EditorGUILayout.LabelField("DEM Preview", EditorStyles.boldLabel);

        var previewSource = GetDemPreviewSource(settings);
        EditorGUILayout.LabelField("Preview Source", string.IsNullOrWhiteSpace(previewSource) ? "(not set)" : previewSource);
        EditorGUILayout.HelpBox(
            "Generate a grayscale preview from the current DEM GeoTIFF to validate the downloaded terrain coverage before export.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(previewSource)))
            {
                if (GUILayout.Button("Load Preview"))
                {
                    TryLoadDemPreview(previewSource);
                }

                if (GUILayout.Button("Refresh Preview"))
                {
                    TryLoadDemPreview(previewSource, forceRefresh: true);
                }
            }

            using (new EditorGUI.DisabledScope(demPreviewTexture == null))
            {
                if (GUILayout.Button("Clear Preview"))
                {
                    ReleasePreviewTexture();
                    demPreviewStatus = "DEM preview cleared.";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(previewSource))
        {
            EditorGUILayout.HelpBox("Download or store a DEM GeoTIFF first to enable preview generation.", MessageType.Info);
        }
        else if (demPreviewTexture != null)
        {
            DrawPreviewTexture(demPreviewTexture);
        }

        EditorGUILayout.HelpBox(demPreviewStatus, demPreviewTexture != null ? MessageType.None : MessageType.Info);
    }

    private void DrawSourcePreviewSection(TerrainForgeWorkflowSettings settings)
    {
        EditorGUILayout.LabelField("Source Preview", EditorStyles.boldLabel);

        var previewSource = GetSourcePreviewSource(settings);
        EditorGUILayout.LabelField("Preview Source", string.IsNullOrWhiteSpace(previewSource) ? "(not set)" : previewSource);
        EditorGUILayout.HelpBox(
            "Load a preview of the selected source file to validate the local raster before copying or refilling bounds.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(previewSource)))
            {
                if (GUILayout.Button("Load Preview"))
                {
                    TryLoadSourcePreview(previewSource);
                }

                if (GUILayout.Button("Refresh Preview"))
                {
                    TryLoadSourcePreview(previewSource, forceRefresh: true);
                }
            }

            using (new EditorGUI.DisabledScope(sourcePreviewTexture == null))
            {
                if (GUILayout.Button("Clear Preview"))
                {
                    ReleaseSourcePreviewTexture();
                    sourcePreviewStatus = "Source preview cleared.";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(previewSource))
        {
            EditorGUILayout.HelpBox("Select a local source file first to enable preview generation.", MessageType.Info);
        }
        else if (sourcePreviewTexture != null)
        {
            DrawPreviewTexture(sourcePreviewTexture);
        }

        EditorGUILayout.HelpBox(sourcePreviewStatus, sourcePreviewTexture != null ? MessageType.None : MessageType.Info);
    }

    private void DrawSatellitePreviewSection(TerrainForgeWorkflowSettings settings)
    {
        EditorGUILayout.LabelField("Sat Preview", EditorStyles.boldLabel);

        var previewSource = GetSatellitePreviewSource(settings);
        EditorGUILayout.LabelField("Preview Source", string.IsNullOrWhiteSpace(previewSource) ? "(not set)" : previewSource);
        EditorGUILayout.HelpBox(
            "Load the current satellite image preview to validate imagery coverage and framing for the selected map bounds.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(previewSource)))
            {
                if (GUILayout.Button("Load Preview"))
                {
                    TryLoadSatellitePreview(previewSource);
                }

                if (GUILayout.Button("Refresh Preview"))
                {
                    TryLoadSatellitePreview(previewSource, forceRefresh: true);
                }
            }

            using (new EditorGUI.DisabledScope(satellitePreviewTexture == null))
            {
                if (GUILayout.Button("Clear Preview"))
                {
                    ReleaseSatellitePreviewTexture();
                    satellitePreviewStatus = "Satellite preview cleared.";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(previewSource))
        {
            EditorGUILayout.HelpBox("Download or store a satellite image first to enable preview generation.", MessageType.Info);
        }
        else if (satellitePreviewTexture != null)
        {
            DrawPreviewTexture(satellitePreviewTexture);
        }

        EditorGUILayout.HelpBox(satellitePreviewStatus, satellitePreviewTexture != null ? MessageType.None : MessageType.Info);
    }

    private void DrawPreviewTexture(Texture2D texture)
    {
        var maxWidth = Mathf.Min(320f, EditorGUIUtility.currentViewWidth * 0.28f);
        var aspect = texture.height > 0 ? texture.width / (float)texture.height : 1f;
        var rect = GUILayoutUtility.GetRect(maxWidth, maxWidth / Mathf.Max(0.01f, aspect), GUILayout.ExpandWidth(false));
        EditorGUI.DrawPreviewTexture(rect, texture, null, ScaleMode.ScaleToFit);
        EditorGUILayout.LabelField("Preview Size", $"{texture.width} x {texture.height}");
    }

    private static void BrowseLocalSource(TerrainForgeWorkflowSettings settings)
    {
        var startFolder = string.IsNullOrWhiteSpace(settings.localSourcePath)
            ? TerrainForgeWindowUtility.ResolveFolderPath("Assets")
            : Path.GetDirectoryName(TerrainForgeWindowUtility.ResolveFolderPath(settings.localSourcePath));

        var selected = EditorUtility.OpenFilePanel("Select GeoTIFF or KAP Source", startFolder, string.Empty);
        if (string.IsNullOrEmpty(selected))
        {
            return;
        }

        settings.localSourcePath = selected;
        var extension = Path.GetExtension(selected).ToLowerInvariant();
        if (extension == ".kap")
        {
            settings.localSourceType = TerrainForgerLocalSourceType.KapChart;
        }
        else
        {
            settings.localSourceType = TerrainForgerLocalSourceType.GeoTiff;
        }
        settings.SaveSettings();
    }

    private static void RunStoreLocalSource(TerrainForgeWorkflowSettings settings)
    {
        try
        {
            TerrainForgerGisDataUtility.StoreLocalSource(settings);
            EditorUtility.DisplayDialog("Local Source Stored", "The source file was copied into Assets/Terrain.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Store Source Failed", ex.Message, "OK");
        }
    }

    private static void RunRefillBoundsFromSource(TerrainForgeWorkflowSettings settings)
    {
        try
        {
            TerrainForgerGisDataUtility.RefillBoundsFromLocalSource(settings);
            EditorUtility.DisplayDialog("Bounds Updated", "The bounds were refilled from the selected source file.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Refill Bounds Failed", ex.Message, "OK");
        }
    }

    private static void RunDownloadDem(TerrainForgeWorkflowSettings settings)
    {
        try
        {
            TerrainForgerGisDataUtility.DownloadDem(settings);
            EditorUtility.DisplayDialog("DEM Download Complete", "The DEM GeoTIFF was saved in Assets/Terrain/GeoTIFF. You can now load its preview in the DEM Preview section.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("DEM Download Failed", ex.Message, "OK");
        }
    }

    private static void RunDownloadSatellite(TerrainForgeWorkflowSettings settings)
    {
        try
        {
            var plan = TerrainForgerGisDataUtility.BuildSatelliteDownloadPlan(settings);
            TerrainForgerGisDataUtility.DownloadSatellite(settings);
            var detail = plan.isValid && plan.requiresTiling
                ? $" The request used a {plan.tilesX} x {plan.tilesY} tile mosaic."
                : string.Empty;
            EditorUtility.DisplayDialog("Satellite Download Complete", $"The satellite image was saved in Assets/Terrain/SAT.{detail}", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Satellite Download Failed", ex.Message, "OK");
        }
    }

    private static void RunDownloadAll(TerrainForgeWorkflowSettings settings)
    {
        try
        {
            TerrainForgerGisDataUtility.DownloadSelectedData(settings);
            EditorUtility.DisplayDialog("GIS Data Complete", "The selected GIS data sources were processed and saved under Assets/Terrain.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("GIS Data Failed", ex.Message, "OK");
        }
    }

    private string GetDemPreviewSource(TerrainForgeWorkflowSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.lastDemGeoTiffPath))
        {
            return settings.lastDemGeoTiffPath;
        }

        if (!string.IsNullOrWhiteSpace(settings.geoTiffPath))
        {
            return settings.geoTiffPath;
        }

        return string.Empty;
    }

    private string GetSourcePreviewSource(TerrainForgeWorkflowSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.localSourcePath) ? string.Empty : settings.localSourcePath;
    }

    private string GetSatellitePreviewSource(TerrainForgeWorkflowSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.lastSatelliteImagePath) ? string.Empty : settings.lastSatelliteImagePath;
    }

    private void TryLoadDemPreview(string previewSource, bool forceRefresh = false)
    {
        if (!forceRefresh &&
            demPreviewTexture != null &&
            string.Equals(demPreviewSourcePath, previewSource, System.StringComparison.OrdinalIgnoreCase))
        {
            demPreviewStatus = "DEM preview is already up to date.";
            return;
        }

        try
        {
            ReleasePreviewTexture();
            demPreviewTexture = TerrainForgerGisDataUtility.CreateDemPreviewTexture(previewSource);
            demPreviewSourcePath = previewSource;
            demPreviewStatus = "DEM preview generated successfully.";
            Repaint();
        }
        catch (System.Exception ex)
        {
            ReleasePreviewTexture();
            demPreviewStatus = $"DEM preview failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private void TryLoadSourcePreview(string previewSource, bool forceRefresh = false)
    {
        if (!forceRefresh &&
            sourcePreviewTexture != null &&
            string.Equals(sourcePreviewSourcePath, previewSource, System.StringComparison.OrdinalIgnoreCase))
        {
            sourcePreviewStatus = "Source preview is already up to date.";
            return;
        }

        try
        {
            ReleaseSourcePreviewTexture();
            sourcePreviewTexture = TerrainForgerGisDataUtility.CreateSourcePreviewTexture(previewSource);
            sourcePreviewSourcePath = previewSource;
            sourcePreviewStatus = "Source preview generated successfully.";
            Repaint();
        }
        catch (System.Exception ex)
        {
            ReleaseSourcePreviewTexture();
            sourcePreviewStatus = $"Source preview failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private void ReleaseSourcePreviewTexture()
    {
        if (sourcePreviewTexture != null)
        {
            DestroyImmediate(sourcePreviewTexture);
            sourcePreviewTexture = null;
        }

        sourcePreviewSourcePath = string.Empty;
    }

    private void ReleasePreviewTexture()
    {
        if (demPreviewTexture != null)
        {
            DestroyImmediate(demPreviewTexture);
            demPreviewTexture = null;
        }

        demPreviewSourcePath = string.Empty;
    }

    private void TryLoadSatellitePreview(string previewSource, bool forceRefresh = false)
    {
        if (!forceRefresh &&
            satellitePreviewTexture != null &&
            string.Equals(satellitePreviewSourcePath, previewSource, System.StringComparison.OrdinalIgnoreCase))
        {
            satellitePreviewStatus = "Satellite preview is already up to date.";
            return;
        }

        try
        {
            ReleaseSatellitePreviewTexture();
            satellitePreviewTexture = TerrainForgerGisDataUtility.CreateSourcePreviewTexture(previewSource);
            satellitePreviewSourcePath = previewSource;
            satellitePreviewStatus = "Satellite preview generated successfully.";
            Repaint();
        }
        catch (System.Exception ex)
        {
            ReleaseSatellitePreviewTexture();
            satellitePreviewStatus = $"Satellite preview failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private void ReleaseSatellitePreviewTexture()
    {
        if (satellitePreviewTexture != null)
        {
            DestroyImmediate(satellitePreviewTexture);
            satellitePreviewTexture = null;
        }

        satellitePreviewSourcePath = string.Empty;
    }
}
